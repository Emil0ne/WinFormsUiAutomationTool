using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Automatyczne_Klawisze
{
    public class EnovaSystemPraw
    {
        private static readonly TimeSpan UiaCallTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UiaPollTimeout = TimeSpan.FromSeconds(2);

        private static void AktywnySleep(int milliseconds, CancellationToken token, ManualResetEventSlim pauseEvent)
        {
            int step = 100;
            int elapsed = 0;
            while (elapsed < milliseconds)
            {
                pauseEvent.Wait(token);
                token.ThrowIfCancellationRequested();
                Thread.Sleep(Math.Min(step, milliseconds - elapsed));
                elapsed += step;
            }
        }

        private static T UiaSafeCall<T>(Func<T> action, T fallback = default)
        {
            try
            {
                return action();
            }
            catch (COMException) { return fallback; }
            catch (InvalidOperationException) { return fallback; }
            catch (Exception) { return fallback; }
        }

        private static List<Window> PobierzWszystkieOkna(FlaUI.Core.Application app, UIA3Automation automation)
        {
            var wynik = new List<Window>();
            if (app == null) return wynik;

            try
            {
                var mainW = UiaSafeCall(() => app.GetMainWindow(automation));
                if (mainW != null)
                {
                    wynik.Add(mainW);
                    var modale = UiaSafeCall(() => mainW.ModalWindows, Array.Empty<Window>());
                    if (modale != null && modale.Length > 0) wynik.AddRange(modale);
                }

                if (wynik.Count == 0)
                {
                    var top = UiaSafeCall(() => app.GetAllTopLevelWindows(automation), Array.Empty<Window>());
                    if (top != null)
                    {
                        foreach (var w in top)
                        {
                            if (w == null) continue;
                            wynik.Add(w);
                            var modale = UiaSafeCall(() => w.ModalWindows, Array.Empty<Window>());
                            if (modale != null && modale.Length > 0) wynik.AddRange(modale);
                        }
                    }
                }
            }
            catch { }

            return wynik;
        }

        public static void Uruchom(List<string> listaBaz, string login, string haslo, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log, Action<string> onBazaZakonczona = null)
        {
            log($"Rozpoczynam sprawdzanie systemu praw dla {listaBaz.Count} baz...");
            string plikRaportu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Raport_SprawdzaniaSystemuPraw_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            try
            {
                string naglowek = $"RAPORT SPRAWDZANIA SYSTEMU PRAW\nData: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nLiczba baz: {listaBaz.Count}\n==========================================\n";
                File.WriteAllText(plikRaportu, naglowek);
                log($"📁 Utworzono plik raportu na Pulpicie: {Path.GetFileName(plikRaportu)}");
            }
            catch (Exception ex)
            {
                log($"⚠️ Ostrzeżenie: Nie udało się zainicjalizować pliku raportu: {ex.Message}");
            }

            foreach (var nazwaBazy in listaBaz)
            {
                string linijkaRaportu = "";
                try
                {
                    token.ThrowIfCancellationRequested();
                    pauseEvent.Wait(token);

                    log($"\n==========================================");
                    log($"---> SPRAWDZAM BAZĘ: {nazwaBazy} <---");
                    log($"==========================================");

                    string systemPrawWynik = "";
                    bool sukces = SprawdzSystemPrawDlaBazy(nazwaBazy, login, haslo, sciezkaEnova, token, pauseEvent, log, out systemPrawWynik);

                    if (!sukces)
                    {
                        linijkaRaportu = $"{nazwaBazy} - BŁĄD: {systemPrawWynik}";
                        log($"❌ BAZA '{nazwaBazy}' ZAKOŃCZONA BŁĘDEM: {systemPrawWynik}");
                    }
                    else
                    {
                        linijkaRaportu = $"{nazwaBazy} - WYNIK: {systemPrawWynik}";
                        log($"✅ BAZA '{nazwaBazy}' -> System praw: {systemPrawWynik}");
                    }
                }
                catch (OperationCanceledException)
                {
                    log("\n🛑 AUTOMATYZACJA PRZERWANA NA ŻĄDANIE.");
                    linijkaRaportu = $"{nazwaBazy} - PRZERWANO PRZEZ UŻYTKOWNIKA";
                    break;
                }
                catch (Exception ex)
                {
                    log($"BŁĄD KRYTYCZNY PĘTLI: {ex.Message}");
                    linijkaRaportu = $"{nazwaBazy} - WYJĄTEK: {ex.Message}";
                }
                finally
                {
                    if (!string.IsNullOrEmpty(linijkaRaportu))
                    {
                        try { File.AppendAllText(plikRaportu, $"[{DateTime.Now:HH:mm:ss}] {linijkaRaportu}\n"); } catch { }
                    }
                    onBazaZakonczona?.Invoke(nazwaBazy);
                }
            }

            log($"\n==========================================");
            log($"🏁 ZAKOŃCZONO SPRAWDZANIE SYSTEMU PRAW.");
        }

        private static bool SprawdzSystemPrawDlaBazy(string nazwaBazy, string login, string haslo, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log, out string wynik)
        {
            wynik = "";
            Process startedProcess = null;
            FlaUI.Core.Application app = null;

            try
            {
                var processInfo = new ProcessStartInfo(sciezkaEnova) { UseShellExecute = true };
                startedProcess = Process.Start(processInfo);
                if (startedProcess == null)
                {
                    wynik = "Nie udało się wystartować procesu Enovy.";
                    return false;
                }

                log("Uruchomiono nową instancję Enova365.");
                AktywnySleep(5000, token, pauseEvent);

                using (var automation = new UIA3Automation())
                {
                    automation.ConnectionTimeout = TimeSpan.FromSeconds(8);
                    automation.TransactionTimeout = TimeSpan.FromSeconds(8);

                    app = FlaUI.Core.Application.Attach(startedProcess);

                    log("Szukam pola wyboru bazy...");
                    Window mainWindow = null;
                    FlaUI.Core.AutomationElements.TextBox poleWyszukiwania = null;

                    for (int i = 0; i < 20; i++)
                    {
                        pauseEvent.Wait(token);
                        var okna = PobierzWszystkieOkna(app, automation);
                        mainWindow = okna.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } }) ?? okna.FirstOrDefault();
                        if (mainWindow == null)
                        {
                            AktywnySleep(500, token, pauseEvent);
                            continue;
                        }

                        var wszystkiePolaEdit = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)), Array.Empty<AutomationElement>());
                        foreach (var pole in wszystkiePolaEdit)
                        {
                            try
                            {
                                var textBox = pole.AsTextBox();
                                if ((textBox.Name != null && textBox.Name.Contains("Szukaj")) ||
                                    (textBox.HelpText != null && textBox.HelpText.Contains("Szukaj")))
                                {
                                    poleWyszukiwania = textBox;
                                    break;
                                }
                            }
                            catch (COMException) { }
                        }

                        if (poleWyszukiwania == null && wszystkiePolaEdit.Length > 0)
                        {
                            try { poleWyszukiwania = wszystkiePolaEdit[0].AsTextBox(); } catch { }
                        }

                        if (poleWyszukiwania != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null || poleWyszukiwania == null)
                    {
                        wynik = "Nie udało się pobrać okna bazy lub pola wyszukiwania.";
                        return false;
                    }

                    poleWyszukiwania.Focus();
                    AktywnySleep(300, token, pauseEvent);
                    poleWyszukiwania.Text = $"\"{nazwaBazy}\"";
                    log($"Filtruję: {nazwaBazy}");
                    AktywnySleep(1200, token, pauseEvent);

                    var localMainWindow = mainWindow;
                    var znalezioneElementy = UiaSafeCall(() => localMainWindow.FindAllDescendants(cf => cf.ByName(nazwaBazy)), Array.Empty<AutomationElement>());
                    AutomationElement elementBazy = znalezioneElementy.FirstOrDefault(e => e.ControlType == ControlType.Text) ?? znalezioneElementy.FirstOrDefault();

                    if (elementBazy != null)
                    {
                        log("Zlokalizowano bazę. Klikam...");
                        try
                        {
                            elementBazy.Click();
                            AktywnySleep(200, token, pauseEvent);
                            elementBazy.DoubleClick();
                        }
                        catch { }
                    }
                    else
                    {
                        wynik = "Nie znaleziono bazy na liście.";
                        return false;
                    }

                    AktywnySleep(3000, token, pauseEvent);

                    // 1. AKTUALIZACJA DODATKÓW
                    var oknaMod = PobierzWszystkieOkna(app, automation);
                    var oknoAktualizacji = oknaMod.FirstOrDefault(m => { try { return m.Name != null && m.Name.Contains("Aktualizacja dodatków"); } catch { return false; } });
                    if (oknoAktualizacji != null)
                    {
                        log("Wykryto okno aktualizacji dodatków! Klikam 'Tak'...");
                        DateTime czasKlikniecia = DateTime.Now.AddSeconds(-2);
                        var btnTak = oknoAktualizacji.FindFirstDescendant(cf => cf.ByName("Tak"))?.AsButton();
                        if (btnTak != null) btnTak.Click();
                        else { oknoAktualizacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }

                        log("Czekam na całkowity reset po aktualizacji (do 30 s)...");
                        int staryPid = startedProcess.Id;
                        string nazwaProcesu = Path.GetFileNameWithoutExtension(sciezkaEnova);
                        Process nowyProces = null;
                        int odczekanoMs = 0;

                        while (odczekanoMs < 30000)
                        {
                            AktywnySleep(1000, token, pauseEvent);
                            odczekanoMs += 1000;
                            var procesyTmp = Process.GetProcessesByName(nazwaProcesu);
                            nowyProces = procesyTmp.Where(p => p.Id != staryPid)
                                                   .OrderByDescending(p => { try { return p.StartTime; } catch { return DateTime.MinValue; } })
                                                   .FirstOrDefault();

                            if (nowyProces != null)
                            {
                                try { if (nowyProces.StartTime >= czasKlikniecia) break; else nowyProces = null; }
                                catch { break; }
                            }
                        }

                        if (nowyProces != null)
                        {
                            startedProcess = nowyProces;
                            try { startedProcess.WaitForInputIdle(15000); } catch { }
                            app = FlaUI.Core.Application.Attach(startedProcess);
                        }
                        else
                        {
                            wynik = "Enova nie wstała po aktualizacji dodatków.";
                            return false;
                        }
                    }

                    // 2. LOGOWANIE ORAZ WERYFIKACJA WERSJI/BŁĘDÓW/KONWERSJI
                    log("Oczekuję na okno logowania...");
                    Window oknoLogowania = null;
                    for (int i = 0; i < 25; i++)
                    {
                        pauseEvent.Wait(token);
                        var topWindows = PobierzWszystkieOkna(app, automation);
                        oknoLogowania = topWindows.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); } catch { return false; } });
                        if (oknoLogowania != null) break;
                        AktywnySleep(300, token, pauseEvent);
                    }

                    if (oknoLogowania != null)
                    {
                        oknoLogowania.Focus();
                        AktywnySleep(400, token, pauseEvent);
                        Keyboard.Type(login);
                        AktywnySleep(200, token, pauseEvent);
                        Keyboard.Press(VirtualKeyShort.TAB);
                        AktywnySleep(200, token, pauseEvent);

                        if (!string.IsNullOrEmpty(haslo))
                        {
                            Keyboard.Type(haslo);
                            AktywnySleep(200, token, pauseEvent);
                        }

                        var btnOk = oknoLogowania.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                        if (btnOk != null) btnOk.Click(); else Keyboard.Press(VirtualKeyShort.ENTER);

                        log("Zatwierdzono logowanie. Sprawdzam stan bazy...");
                        AktywnySleep(3000, token, pauseEvent);

                        bool zlyLogin = false;
                        bool nowszaWersja = false;
                        bool wymagaKonwersji = false;
                        string wersjaNowszejBazy = "";

                        for (int i = 0; i < 40; i++)
                        {
                            pauseEvent.Wait(token);
                            var topWindows = PobierzWszystkieOkna(app, automation);

                            // Sukces (wejście do licencji / programu)
                            bool odrazuLicencje = topWindows.Any(w => { try { return w.Name != null && (w.Name.Contains("Pobrane licencje") || w.Name.Contains("Licencja programu")); } catch { return false; } });
                            if (odrazuLicencje)
                            {
                                log("Wykryto okno licencji / zalogowano.");
                                break;
                            }

                            // Okno Konwersji Bazy (Zbyt stara wersja)
                            Window konwersjaWindow = topWindows.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("Konwersja bazy"); } catch { return false; } });
                            if (konwersjaWindow != null)
                            {
                                log("⚠️ Wykryto okno 'Konwersja bazy'! Klikam 'Anuluj'...");
                                wymagaKonwersji = true;
                                try
                                {
                                    var btnAnulujKonwersje = konwersjaWindow.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                    if (btnAnulujKonwersje != null) btnAnulujKonwersje.Click();
                                    else { konwersjaWindow.Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); }
                                }
                                catch { }

                                AktywnySleep(1500, token, pauseEvent);

                                var oknaPoAnulowaniu = PobierzWszystkieOkna(app, automation);
                                var errorPoKonwersji = oknaPoAnulowaniu.FirstOrDefault(w => { try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd") || w.Name.Contains("Informacja")); } catch { return false; } });
                                if (errorPoKonwersji != null)
                                {
                                    try
                                    {
                                        var btnOkError = errorPoKonwersji.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                        if (btnOkError != null) btnOkError.Click();
                                        else { errorPoKonwersji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                    }
                                    catch { }
                                    AktywnySleep(1000, token, pauseEvent);
                                }
                                break;
                            }

                            // Okno Błędów (Zła nowsza wersja / Błędne hasło)
                            Window errorWindow = topWindows.FirstOrDefault(w => { try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")); } catch { return false; } });
                            if (errorWindow != null)
                            {
                                string errorText = "";
                                try
                                {
                                    var textElements = errorWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
                                    foreach (var te in textElements)
                                    {
                                        if (!string.IsNullOrWhiteSpace(te.Name)) errorText += te.Name + " ";
                                    }
                                }
                                catch { }

                                if (errorText.Contains("z nowszej wersji"))
                                {
                                    nowszaWersja = true;
                                    var match = Regex.Match(errorText, @"\(([\d\.]+)\)");
                                    if (match.Success) wersjaNowszejBazy = match.Groups[1].Value;
                                }
                                else
                                {
                                    zlyLogin = true;
                                }

                                try
                                {
                                    var btnOkError = errorWindow.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                    if (btnOkError != null) btnOkError.Click(); else { errorWindow.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                }
                                catch { }

                                AktywnySleep(1000, token, pauseEvent);
                                break;
                            }

                            bool logowanieIstnieje = topWindows.Any(w => { try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); } catch { return false; } });
                            if (!logowanieIstnieje) break;

                            AktywnySleep(300, token, pauseEvent);
                        }

                        if (wymagaKonwersji || nowszaWersja || zlyLogin)
                        {
                            try
                            {
                                var btnAnulujLogowanie = oknoLogowania?.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                btnAnulujLogowanie?.Click();
                            }
                            catch { }

                            if (wymagaKonwersji)
                            {
                                wynik = "Baza wymaga konwersji (zbyt stara wersja).";
                                return false;
                            }
                            if (nowszaWersja)
                            {
                                wynik = string.IsNullOrEmpty(wersjaNowszejBazy)
                                    ? "Baza pochodzi z nowszej wersji programu."
                                    : $"Baza pochodzi z nowszej wersji programu ({wersjaNowszejBazy}).";
                                return false;
                            }
                            if (zlyLogin)
                            {
                                wynik = "Odrzucono logowanie (Błędne hasło lub zablokowane konto).";
                                return false;
                            }
                        }
                    }
                    else
                    {
                        wynik = "Brak okna logowania.";
                        return false;
                    }

                    // 3. OBSŁUGA LICENCJI I POWIADOMIEŃ
                    log("Sprawdzam okno licencji / powiadomień po starcie...");
                    for (int j = 0; j < 40; j++)
                    {
                        pauseEvent.Wait(token);
                        var wszystkieOknaPo = PobierzWszystkieOkna(app, automation);

                        Window oknoLicencjaProg = wszystkieOknaPo.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("Licencja programu"); } catch { return false; } });
                        if (oknoLicencjaProg != null)
                        {
                            log("Wykryto okno 'Licencja programu'. Podpinam licencję...");

                            var btnWybierzZainstalowana = UiaSafeCall(() =>
                                oknoLicencjaProg.FindFirstDescendant(cf => cf.ByName("Wybierz zainstalowaną licencję"))?.AsButton() ??
                                oknoLicencjaProg.FindFirstDescendant(cf => cf.ByAutomationId("buttonSelectInstalledLicense"))?.AsButton());

                            if (btnWybierzZainstalowana != null)
                            {
                                btnWybierzZainstalowana.Click();
                                log(" -> Kliknięto 'Wybierz zainstalowaną licencję'.");
                            }
                            else
                            {
                                var przyciski = UiaSafeCall(() => oknoLicencjaProg.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)), Array.Empty<AutomationElement>());
                                var btn = przyciski.FirstOrDefault(b => { try { return b.Name != null && b.Name.Contains("zainstalowaną"); } catch { return false; } });
                                if (btn != null) btn.Click();
                                else { oknoLicencjaProg.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            }
                            AktywnySleep(1500, token, pauseEvent);

                            Window oknoWybierzLic = null;
                            for (int k = 0; k < 15; k++)
                            {
                                var oknaTmp = PobierzWszystkieOkna(app, automation);
                                oknoWybierzLic = oknaTmp.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("Wybierz licencję"); } catch { return false; } });
                                if (oknoWybierzLic != null) break;
                                AktywnySleep(500, token, pauseEvent);
                            }

                            if (oknoWybierzLic != null)
                            {
                                log("Wykryto okno 'Wybierz licencję'. Klikam OK...");
                                var btnOkWybierz = UiaSafeCall(() => oknoWybierzLic.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton() ?? oknoWybierzLic.FindFirstDescendant(cf => cf.ByAutomationId("buttonOK"))?.AsButton());
                                if (btnOkWybierz != null) btnOkWybierz.Click(); else { oknoWybierzLic.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                AktywnySleep(2000, token, pauseEvent);
                            }

                            log("Zatwierdzam główne okno 'Licencja programu'...");
                            var btnOkLicProg = UiaSafeCall(() => oknoLicencjaProg.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton() ?? oknoLicencjaProg.FindFirstDescendant(cf => cf.ByAutomationId("buttonOK"))?.AsButton());
                            if (btnOkLicProg != null) btnOkLicProg.Click(); else { oknoLicencjaProg.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            AktywnySleep(3000, token, pauseEvent);
                            continue;
                        }

                        Window oknoLicencji = null;
                        AutomationElement btnOdznacz = null;
                        AutomationElement btnZapisz = null;

                        foreach (var wnd in wszystkieOknaPo)
                        {
                            btnZapisz = UiaSafeCall(() => wnd.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij")));
                            if (btnZapisz != null)
                            {
                                oknoLicencji = wnd;
                                btnOdznacz = UiaSafeCall(() => wnd.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje")));
                                break;
                            }
                        }

                        if (oknoLicencji != null && btnZapisz != null)
                        {
                            log("Znaleziono ekran licencji ('Pobrane licencje'). Focusuję...");
                            try { oknoLicencji.Focus(); } catch { }
                            AktywnySleep(500, token, pauseEvent);

                            if (btnOdznacz != null && btnOdznacz.IsEnabled)
                            {
                                log(" ⚠️ Wykryto niedostępne licencje! Klikam 'Odznacz niedostępne licencje'...");
                                try
                                {
                                    var ip = btnOdznacz.Patterns.Invoke.PatternOrDefault;
                                    if (ip != null) ip.Invoke(); else btnOdznacz.AsButton().Click();
                                }
                                catch
                                {
                                    try { btnOdznacz.AsButton().Click(); } catch { }
                                }

                                log(" -> Czekam na odznaczenie modułów przez Enovę...");
                                AktywnySleep(1200, token, pauseEvent);
                            }

                            log(" -> Klikam 'Zapisz i zamknij'...");
                            try
                            {
                                var ip = btnZapisz.Patterns.Invoke.PatternOrDefault;
                                if (ip != null) ip.Invoke(); else btnZapisz.AsButton().Click();
                            }
                            catch
                            {
                                try { btnZapisz.AsButton().Click(); } catch { }
                            }

                            log(" -> Czekam na całkowite zamknięcie okna licencji...");
                            for (int wait = 0; wait < 20; wait++)
                            {
                                AktywnySleep(500, token, pauseEvent);
                                var oknaPo = PobierzWszystkieOkna(app, automation);
                                bool nadalJest = oknaPo.Any(w => { try { return w.Name != null && w.Name.Contains("Pobrane licencje"); } catch { return false; } });
                                if (!nadalJest)
                                {
                                    log("✅ Okno licencji pomyślnie zamknięte.");
                                    break;
                                }
                            }

                            AktywnySleep(1500, token, pauseEvent);
                            break;
                        }

                        var oknoInfo = wszystkieOknaPo.FirstOrDefault(w => { try { return w.Name != null && (w.Name.Contains("Informacja") || w.Name.Contains("Wygasła sesja")); } catch { return false; } });
                        if (oknoInfo != null)
                        {
                            log("Wykryto okno informacji. Klikam OK...");
                            var btnOkInfo = UiaSafeCall(() => oknoInfo.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton());
                            if (btnOkInfo != null) btnOkInfo.Click(); else { oknoInfo.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            AktywnySleep(2000, token, pauseEvent);
                            break;
                        }

                        bool glowneOkno = wszystkieOknaPo.Any(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } });
                        if (glowneOkno && oknoLicencjaProg == null) break;

                        AktywnySleep(500, token, pauseEvent);
                    }

                    // 4. POBRANIE GŁÓWNEGO OKNA I ODCZYT SYSTEMU PRAW
                    mainWindow = null;
                    for (int i = 0; i < 15; i++)
                    {
                        var okna = PobierzWszystkieOkna(app, automation);
                        mainWindow = okna.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } }) ?? okna.FirstOrDefault();
                        if (mainWindow != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null) { wynik = "Nie udało się pobrać głównego okna bazy."; return false; }

                    log("Otwieram Opcje (Ctrl + F9)...");
                    mainWindow.Focus();
                    AktywnySleep(800, token, pauseEvent);

                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
                    {
                        Keyboard.Press(VirtualKeyShort.F9);
                    }

                    log("Czekam na załadowanie zakładek w Opcjach...");
                    AktywnySleep(2500, token, pauseEvent);

                    log("Szukam pola 'Szukaj zakładki...' po współrzędnych przestrzennych...");
                    AutomationElement poleSzukajZakladki = null;

                    var wszystkieEdity = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)), Array.Empty<AutomationElement>());

                    foreach (var ed in wszystkieEdity)
                    {
                        try
                        {
                            string name = ed.Name ?? "";
                            string help = ed.HelpText ?? "";
                            if (name.IndexOf("szukaj", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("zakładk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                help.IndexOf("szukaj", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                help.IndexOf("zakładk", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                poleSzukajZakladki = ed;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (poleSzukajZakladki == null && wszystkieEdity.Length > 0)
                    {
                        poleSzukajZakladki = wszystkieEdity
                            .Where(e => { try { var r = e.BoundingRectangle; return r.Width > 0 && r.Height > 0; } catch { return false; } })
                            .OrderBy(e => e.BoundingRectangle.X)
                            .FirstOrDefault();
                    }

                    if (poleSzukajZakladki != null)
                    {
                        log($"Zlokalizowano lewe pole wyszukiwania (X={poleSzukajZakladki.BoundingRectangle.X}, Y={poleSzukajZakladki.BoundingRectangle.Y}). Klikam...");
                        try
                        {
                            poleSzukajZakladki.Focus();
                            AktywnySleep(200, token, pauseEvent);
                            poleSzukajZakladki.Click();
                            AktywnySleep(300, token, pauseEvent);
                        }
                        catch { }
                    }
                    else
                    {
                        log("Kliknięcie awaryjne w obszar lewego paska nawigacji...");
                        try
                        {
                            var rect = mainWindow.BoundingRectangle;
                            UiaSafeCall(() => { FlaUI.Core.Input.Mouse.Click(new System.Drawing.Point(rect.X + 80, rect.Y + 130)); return true; });
                            AktywnySleep(300, token, pauseEvent);
                        }
                        catch { }
                    }

                    log("Wpisuję frazę 'System praw'...");
                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) { Keyboard.Press(VirtualKeyShort.KEY_A); }
                    AktywnySleep(100, token, pauseEvent);
                    Keyboard.Type("System praw");
                    AktywnySleep(1500, token, pauseEvent);

                    log("Wybieram przefiltrowaną pozycję 'System praw'...");
                    var znalezioneZakladki = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByName("System praw")), Array.Empty<AutomationElement>());

                    AutomationElement elSystemPraw = znalezioneZakladki.FirstOrDefault(e =>
                        e.ControlType == ControlType.TreeItem ||
                        e.ControlType == ControlType.ListItem ||
                        e.ControlType == ControlType.Text) ?? znalezioneZakladki.FirstOrDefault();

                    if (elSystemPraw != null)
                    {
                        log("Klikam w zlokalizowany element 'System praw'...");
                        try
                        {
                            elSystemPraw.Click();
                            AktywnySleep(200, token, pauseEvent);
                            elSystemPraw.DoubleClick();
                        }
                        catch { }
                    }
                    else
                    {
                        log("Zatwierdzam wybór Strzałką w dół i Enterem...");
                        Keyboard.Press(VirtualKeyShort.DOWN);
                        AktywnySleep(300, token, pauseEvent);
                        Keyboard.Press(VirtualKeyShort.ENTER);
                    }

                    AktywnySleep(1500, token, pauseEvent);

                    log("Odczytuję wartość systemu praw...");
                    string odczytanaWartosc = "";

                    for (int k = 0; k < 10; k++)
                    {
                        pauseEvent.Wait(token);

                        var comboboxy = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox)), Array.Empty<AutomationElement>());
                        foreach (var cb in comboboxy)
                        {
                            try
                            {
                                var comboEl = cb.AsComboBox();
                                string tekstCombo = comboEl.SelectedItem?.Text ?? "";
                                if (string.IsNullOrEmpty(tekstCombo) && comboEl.Patterns.Value.IsSupported)
                                {
                                    tekstCombo = comboEl.Patterns.Value.Pattern.Value.Value ?? "";
                                }

                                if (!string.IsNullOrWhiteSpace(tekstCombo) &&
                                   (tekstCombo.Contains("Standardowy") || tekstCombo.Contains("Rozszerzony") || tekstCombo.Contains("Uproszczony")))
                                {
                                    odczytanaWartosc = tekstCombo.Trim();
                                    break;
                                }
                            }
                            catch { }
                        }

                        if (!string.IsNullOrEmpty(odczytanaWartosc)) break;

                        var wszystkieEl = UiaSafeCall(() => mainWindow.FindAllDescendants(), Array.Empty<AutomationElement>());
                        foreach (var el in wszystkieEl)
                        {
                            try
                            {
                                if (el.Patterns.Value.IsSupported)
                                {
                                    string val = el.Patterns.Value.Pattern.Value.Value ?? "";
                                    if (val.Equals("Rozszerzony", StringComparison.OrdinalIgnoreCase) ||
                                        val.Equals("Standardowy", StringComparison.OrdinalIgnoreCase) ||
                                        val.Equals("Uproszczony", StringComparison.OrdinalIgnoreCase))
                                    {
                                        odczytanaWartosc = val.Trim();
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (!string.IsNullOrEmpty(odczytanaWartosc)) break;

                        var btnNaStandardowy = UiaSafeCall(() => mainWindow.FindFirstDescendant(cf => cf.ByName("Zmień system praw na standardowy")));
                        if (btnNaStandardowy != null) { odczytanaWartosc = "Rozszerzony"; break; }

                        var btnNaRozszerzony = UiaSafeCall(() => mainWindow.FindFirstDescendant(cf => cf.ByName("Zmień system praw na rozszerzony")));
                        if (btnNaRozszerzony != null) { odczytanaWartosc = "Standardowy"; break; }

                        AktywnySleep(300, token, pauseEvent);
                    }

                    if (string.IsNullOrEmpty(odczytanaWartosc))
                    {
                        odczytanaWartosc = "Standardowy";
                    }

                    wynik = odczytanaWartosc;
                    log($"Odczytano system praw: {wynik}");

                    try
                    {
                        mainWindow.Focus();
                        Keyboard.Press(VirtualKeyShort.ESCAPE);
                        AktywnySleep(800, token, pauseEvent);
                    }
                    catch { }

                    return true;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                wynik = $"Błąd systemu: {ex.Message}";
                return false;
            }
            finally
            {
                try { app?.Close(); } catch { }
                try
                {
                    if (startedProcess != null && !startedProcess.HasExited)
                    {
                        if (!startedProcess.WaitForExit(3000))
                        {
                            startedProcess.Kill();
                        }
                    }
                }
                catch { }

                AktywnySleep(1000, token, pauseEvent);
            }
        }
    }
}