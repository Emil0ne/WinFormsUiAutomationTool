using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace Automatyczne_Klawisze
{
    public class EnovaKonwersjaPraw
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

        private static T UiaSafeCall<T>(Func<T> action, TimeSpan timeout, T fallback = default)
        {
            try
            {
                var task = Task.Run(action);
                if (task.Wait(timeout)) return task.IsFaulted ? fallback : task.Result;
                return fallback;
            }
            catch { return fallback; }
        }

        private static T UiaSafeCall<T>(Func<T> action, T fallback = default)
        {
            try { return action(); }
            catch { return fallback; }
        }

        private static List<Window> PobierzWszystkieOkna(FlaUI.Core.Application app, UIA3Automation automation)
        {
            var wynik = new List<Window>();
            if (app == null) return wynik;

            try
            {
                // Pobieramy wszystkie okna najwyższego poziomu oraz ich okna modalne
                var top = UiaSafeCall(() => app.GetAllTopLevelWindows(automation), TimeSpan.FromSeconds(3), Array.Empty<Window>());
                if (top != null)
                {
                    foreach (var w in top)
                    {
                        if (w == null) continue;
                        wynik.Add(w);
                        try
                        {
                            var modale = UiaSafeCall(() => w.ModalWindows, TimeSpan.FromSeconds(1), Array.Empty<Window>());
                            if (modale != null && modale.Length > 0) wynik.AddRange(modale);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return wynik;
        }

        public static void Uruchom(
            List<string> listaBaz,
            string login,
            string haslo,
            string sciezkaEnova,
            bool tylkoUzgodnijRole,
            string sqlConnectionString,
            CancellationToken token,
            ManualResetEventSlim pauseEvent,
            Action<string> log,
            Action<string> onBazaZakonczona = null)
        {
            string trybText = tylkoUzgodnijRole ? "TYLKO UZGODNIENIE RÓL" : "PEŁNA KONWERSJA (ROZSZERZONY)";
            log($"Rozpoczynam proces konwersji systemu praw [{trybText}] dla {listaBaz.Count} baz...");

            string plikRaportu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"Raport_KonwersjaPraw_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            try
            {
                string naglowek = $"RAPORT KONWERSJI SYSTEMU PRAW [{trybText}]\nData rozpoczęcia: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nLiczba baz: {listaBaz.Count}\n==================================================\n";
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

                    log("\n==========================================");
                    log($"---> ANALIZA BAZY: {nazwaBazy} <---");
                    log("==========================================");

                    bool pomijajBaze = false;

                    if (!tylkoUzgodnijRole)
                    {
                        log("Sprawdzam aktywne sesje w MS SQL Server (wymagane dla pełnej konwersji)...");
                        bool saZalogowani = SqlChecker.CzyBazaMaAktywnychUzytkownikow(sqlConnectionString, nazwaBazy, out var aktywniUzytkownicy);

                        if (saZalogowani)
                        {
                            log($"⚠️ OSTRZEŻENIE: Baza '{nazwaBazy}' ma aktywnych użytkowników!");
                            foreach (var usr in aktywniUzytkownicy)
                            {
                                log($"   -> {usr}");
                            }
                            log("❌ POMIJAM KONWERSJĘ DLA TEJ BAZY (Zapobieganie uszkodzeniu ról).");
                            linijkaRaportu = $"{nazwaBazy} - POMINIĘTO (Aktywni użytkownicy w SQL)";
                            pomijajBaze = true;
                        }
                        else
                        {
                            log("✅ Baza jest czysta (0 aktywnych sesji).");
                        }
                    }
                    else
                    {
                        log("ℹ️ Tryb 'Tylko uzgodnienie ról' - pomijam blokadę sesji SQL.");
                    }

                    if (pomijajBaze)
                    {
                        continue;
                    }

                    log("Uruchamiam proces Enovy...");
                    bool sukces = KonwertujSystemPrawDlaBazy(nazwaBazy, login, haslo, sciezkaEnova, tylkoUzgodnijRole, token, pauseEvent, log, out string konwersjaWynik);

                    if (!sukces)
                    {
                        linijkaRaportu = $"{nazwaBazy} - BŁĄD: {konwersjaWynik}";
                        log($"❌ BAZA '{nazwaBazy}' ZAKOŃCZONA BŁĘDEM: {konwersjaWynik}");
                    }
                    else
                    {
                        linijkaRaportu = $"{nazwaBazy} - {konwersjaWynik}";
                        log($"✅ BAZA '{nazwaBazy}' -> Status: {konwersjaWynik}");
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
                    linijkaRaportu = $"{nazwaBazy} - WYJĄTEK KRYTYCZNY: {ex.Message}";
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

            log("\n==========================================");
            log("🏁 ZAKOŃCZONO PROCES KONWERSJI SYSTEMU PRAW.");
        }

        private static bool KonwertujSystemPrawDlaBazy(
            string nazwaBazy,
            string login,
            string haslo,
            string sciezkaEnova,
            bool tylkoUzgodnijRole,
            CancellationToken token,
            ManualResetEventSlim pauseEvent,
            Action<string> log,
            out string wynik)
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
                AktywnySleep(6000, token, pauseEvent);

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

                        var wszystkiePolaEdit = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)), UiaPollTimeout, Array.Empty<AutomationElement>());
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
                    AktywnySleep(500, token, pauseEvent);
                    poleWyszukiwania.Text = $"\"{nazwaBazy}\"";
                    log($"Filtruję: {nazwaBazy}");
                    AktywnySleep(1500, token, pauseEvent);

                    var localMainWindow = mainWindow;
                    var znalezioneElementy = UiaSafeCall(() => localMainWindow.FindAllDescendants(cf => cf.ByName(nazwaBazy)), UiaCallTimeout, Array.Empty<AutomationElement>());
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

                    // ==========================================
                    // KROK 1: AKTUALIZACJA DODATKÓW
                    // ==========================================
                    var modalne1 = UiaSafeCall(() => mainWindow.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                    var oknoAktualizacji = modalne1.FirstOrDefault(m => { try { return m.Name != null && m.Name.Contains("Aktualizacja dodatków"); } catch { return false; } });
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

                    // ==========================================
                    // KROK 2: LOGOWANIE ORAZ WERYFIKACJA BŁĘDÓW
                    // ==========================================
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
                        AktywnySleep(500, token, pauseEvent);
                        Keyboard.Type(login);
                        AktywnySleep(300, token, pauseEvent);
                        Keyboard.Press(VirtualKeyShort.TAB);
                        AktywnySleep(300, token, pauseEvent);

                        if (!string.IsNullOrEmpty(haslo))
                        {
                            Keyboard.Type(haslo);
                            AktywnySleep(300, token, pauseEvent);
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

                            // 1. Sukces (zalogowano)
                            bool odrazuLicencje = topWindows.Any(w => { try { return w.Name != null && (w.Name.Contains("Pobrane licencje") || w.Name.Contains("Licencja programu")); } catch { return false; } });
                            if (odrazuLicencje)
                            {
                                log("Wykryto okno licencji / zalogowano.");
                                break;
                            }

                            // 2. Okno Konwersji Bazy
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
                                AktywnySleep(1000, token, pauseEvent);
                                break;
                            }

                            // 3. Okno Błędów / Złego logowania / Wersji
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

                                log($"⚠️ Wykryto komunikat błędu Enovy: '{errorText.Trim()}'. Klikam OK...");
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

                        // Jeśli wystąpił jakikolwiek problem - zamykamy okno logowania
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

                    // ==========================================
                    // KROK 3: OBSŁUGA LICENCJI I POWIADOMIEŃ
                    // ==========================================
                    log("Sprawdzam okno licencji / powiadomień po starcie...");
                    for (int j = 0; j < 40; j++)
                    {
                        pauseEvent.Wait(token);
                        var wszystkieOknaPo = PobierzWszystkieOkna(app, automation);

                        // 1. Ekran 'Licencja programu'
                        Window oknoLicencjaProg = wszystkieOknaPo.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("Licencja programu"); } catch { return false; } });
                        if (oknoLicencjaProg != null)
                        {
                            log("Wykryto okno 'Licencja programu'. Podpinam licencję...");

                            var btnWybierzZainstalowana = UiaSafeCall(() =>
                                oknoLicencjaProg.FindFirstDescendant(cf => cf.ByName("Wybierz zainstalowaną licencję"))?.AsButton() ??
                                oknoLicencjaProg.FindFirstDescendant(cf => cf.ByAutomationId("buttonSelectInstalledLicense"))?.AsButton(),
                                UiaCallTimeout);

                            if (btnWybierzZainstalowana != null)
                            {
                                btnWybierzZainstalowana.Click();
                                log(" -> Kliknięto 'Wybierz zainstalowaną licencję'.");
                            }
                            else
                            {
                                var przyciski = UiaSafeCall(() => oknoLicencjaProg.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)), UiaPollTimeout, Array.Empty<AutomationElement>());
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
                                var btnOkWybierz = UiaSafeCall(() => oknoWybierzLic.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton() ?? oknoWybierzLic.FindFirstDescendant(cf => cf.ByAutomationId("buttonOK"))?.AsButton(), UiaCallTimeout);
                                if (btnOkWybierz != null) btnOkWybierz.Click(); else { oknoWybierzLic.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                AktywnySleep(2000, token, pauseEvent);
                            }

                            log("Zatwierdzam główne okno 'Licencja programu'...");
                            var btnOkLicProg = UiaSafeCall(() => oknoLicencjaProg.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton() ?? oknoLicencjaProg.FindFirstDescendant(cf => cf.ByAutomationId("buttonOK"))?.AsButton(), UiaCallTimeout);
                            if (btnOkLicProg != null) btnOkLicProg.Click(); else { oknoLicencjaProg.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            AktywnySleep(3000, token, pauseEvent);
                            continue;
                        }

                        // 2. Ekran 'Pobrane licencje'
                        Window oknoLicencji = null;
                        AutomationElement btnOdznacz = null;
                        AutomationElement btnZapisz = null;

                        foreach (var wnd in wszystkieOknaPo)
                        {
                            btnZapisz = UiaSafeCall(() => wnd.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij")), UiaPollTimeout);
                            if (btnZapisz != null)
                            {
                                oknoLicencji = wnd;
                                btnOdznacz = UiaSafeCall(() => wnd.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje")), UiaPollTimeout);
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

                            for (int wait = 0; wait < 20; wait++)
                            {
                                AktywnySleep(500, token, pauseEvent);
                                var oknaPo = PobierzWszystkieOkna(app, automation);
                                bool nadalJest = oknaPo.Any(w => { try { return w.Name != null && w.Name.Contains("Pobrane licencje"); } catch { return false; } });
                                if (!nadalJest) break;
                            }

                            AktywnySleep(1500, token, pauseEvent);
                            break;
                        }

                        // 3. Okna informacyjne
                        var oknoInfo = wszystkieOknaPo.FirstOrDefault(w => { try { return w.Name != null && (w.Name.Contains("Informacja") || w.Name.Contains("Wygasła sesja")); } catch { return false; } });
                        if (oknoInfo != null)
                        {
                            log("Wykryto okno informacji. Klikam OK...");
                            var btnOkInfo = UiaSafeCall(() => oknoInfo.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton(), UiaCallTimeout);
                            if (btnOkInfo != null) btnOkInfo.Click(); else { oknoInfo.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            AktywnySleep(2000, token, pauseEvent);
                            break;
                        }

                        bool glowneOkno = wszystkieOknaPo.Any(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } });
                        if (glowneOkno && oknoLicencjaProg == null) break;

                        AktywnySleep(500, token, pauseEvent);
                    }

                    // Pobranie głównego okna po zalogowaniu
                    mainWindow = null;
                    for (int i = 0; i < 15; i++)
                    {
                        var okna = PobierzWszystkieOkna(app, automation);
                        mainWindow = okna.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } }) ?? okna.FirstOrDefault();
                        if (mainWindow != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null)
                    {
                        wynik = "Nie udało się pobrać głównego okna bazy.";
                        return false;
                    }

                    // ==========================================
                    // KROK 4: KONWERSJA SYSTEMU PRAW
                    // ==========================================
                    if (!OtworzZakladkeSystemPraw(mainWindow, app, automation, token, pauseEvent, log))
                    {
                        wynik = "Nie udało się otworzyć zakładki System praw.";
                        return false;
                    }

                    Window oknoOpcji = SzukajOknaOpcje(app, automation);
                    var oknoRobocze = oknoOpcji ?? mainWindow;

                    string sprawdzonyStan = OdczytajObecnySystemPraw(oknoRobocze, token, pauseEvent);
                    if (sprawdzonyStan.Equals("Rozszerzony", StringComparison.OrdinalIgnoreCase))
                    {
                        log("ℹ️ Baza posiada już system ROZSZERZONY. Pomijam dalsze akcje.");
                        wynik = "Pominięto (Baza miała już system rozszerzony)";
                        try { (oknoOpcji ?? mainWindow).Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); AktywnySleep(800, token, pauseEvent); } catch { }
                        return true;
                    }

                    var btnUzgodnij = UiaSafeCall(() => oknoRobocze.FindFirstDescendant(cf => cf.ByName("Uzgodnij standardowe role"))?.AsButton());
                    bool wykonanoUzgodnienie = false;

                    if (btnUzgodnij != null && btnUzgodnij.IsEnabled)
                    {
                        log("Wykryto konieczność wykonania 'Uzgodnij standardowe role'. Klikam...");
                        btnUzgodnij.Click();
                        AktywnySleep(2000, token, pauseEvent);

                        var oknaWszystkie = PobierzWszystkieOkna(app, automation);
                        var oknoUzgodnijModal = oknaWszystkie.FirstOrDefault(w => { try { return w != oknoRobocze && w != mainWindow; } catch { return false; } });
                        if (oknoUzgodnijModal != null)
                        {
                            log("Zatwierdzam komunikat uzgodnienia ról (OK)...");
                            var btnOkModal = oknoUzgodnijModal.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                            if (btnOkModal != null) btnOkModal.Click();
                            else { oknoUzgodnijModal.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            AktywnySleep(1500, token, pauseEvent);
                        }

                        log("Zapisuję zmiany po uzgodnieniu ról...");
                        ZapiszIZamknijOpcjeI_Czekaj(mainWindow, app, automation, token, pauseEvent, log);
                        wykonanoUzgodnienie = true;
                    }

                    if (tylkoUzgodnijRole)
                    {
                        if (wykonanoUzgodnienie)
                        {
                            wynik = "Pomyślnie wykonano uzgodnienie standardowych ról.";
                            log($"✅ Sukces: {wynik}");
                            return true;
                        }

                        log("ℹ️ Standardowe role były już wcześniej uzgodnione. Zamykam opcje...");
                        try { (oknoOpcji ?? mainWindow).Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); AktywnySleep(800, token, pauseEvent); } catch { }
                        wynik = "Role były już wcześniej uzgodnione.";
                        return true;
                    }

                    if (wykonanoUzgodnienie)
                    {
                        log("Ponownie otwieram Opcje (Ctrl + F9) po uzgodnieniu ról do pełnej konwersji...");
                        if (!OtworzZakladkeSystemPraw(mainWindow, app, automation, token, pauseEvent, log))
                        {
                            wynik = "Nie udało się ponownie otworzyć zakładki System praw po uzgodnieniu ról.";
                            return false;
                        }
                        oknoOpcji = SzukajOknaOpcje(app, automation);
                        oknoRobocze = oknoOpcji ?? mainWindow;
                    }

                    var btnZmienPrawa = UiaSafeCall(() => oknoRobocze.FindFirstDescendant(cf => cf.ByName("Zmień system praw na rozszerzony"))?.AsButton());
                    if (btnZmienPrawa != null && btnZmienPrawa.IsEnabled)
                    {
                        log("Klikam przycisk 'Zmień system praw na rozszerzony'...");
                        btnZmienPrawa.Click();
                        AktywnySleep(1000, token, pauseEvent);

                        bool kliknietoOk = false;
                        for (int attempt = 0; attempt < 15; attempt++)
                        {
                            pauseEvent.Wait(token);
                            var oknaWsz = PobierzWszystkieOkna(app, automation);

                            foreach (var wnd in oknaWsz)
                            {
                                if (wnd == mainWindow || wnd == oknoOpcji) continue;
                                var btn = UiaSafeCall(() => wnd.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton());
                                if (btn != null)
                                {
                                    log("Zatwierdzam okno opcji konwersji (OK)...");
                                    btn.Click();
                                    kliknietoOk = true;
                                    break;
                                }
                            }
                            if (kliknietoOk) break;
                            AktywnySleep(500, token, pauseEvent);
                        }

                        if (!kliknietoOk)
                        {
                            log("Brak przycisku OK pod UIA - wysyłam ENTER...");
                            (oknoOpcji ?? mainWindow).Focus();
                            Keyboard.Press(VirtualKeyShort.ENTER);
                        }

                        CzekajNaZmianeNaRozszerzony(oknoRobocze, token, pauseEvent, log);

                        log("Zapisuję konwersję (Zapisz i zamknij)...");
                        ZapiszIZamknijOpcjeI_Czekaj(mainWindow, app, automation, token, pauseEvent, log);

                        wynik = "Pomyślnie przekonwertowano na system ROZSZERZONY";
                        log($"✅ Sukces: {wynik}");
                        return true;
                    }

                    wynik = "Nie odnaleziono aktywnego przycisku konwersji systemu praw.";
                    return false;
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
                // Twarde, natychmiastowe ubicie procesu, aby nie zostawiać wiszących instancji
                try { app?.Close(); } catch { }
                try
                {
                    if (startedProcess != null && !startedProcess.HasExited)
                    {
                        startedProcess.Kill();
                    }
                }
                catch { }

                AktywnySleep(1000, token, pauseEvent);
            }
        }

        // ==========================================
        // METODY POMOCNICZE
        // ==========================================

        private static Window SzukajOknaOpcje(FlaUI.Core.Application app, UIA3Automation automation)
        {
            var okna = PobierzWszystkieOkna(app, automation);
            return okna.FirstOrDefault(w => {
                try { return (w.AutomationId != null && w.AutomationId.Equals("DataForm", StringComparison.OrdinalIgnoreCase)) || (w.Name != null && w.Name.Contains("Opcje")); }
                catch { return false; }
            });
        }

        private static bool OtworzZakladkeSystemPraw(Window mainWindow, FlaUI.Core.Application app, UIA3Automation automation, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log)
        {
            log("Otwieram Opcje (Ctrl + F9)...");
            mainWindow.Focus();
            AktywnySleep(800, token, pauseEvent);

            using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
            {
                Keyboard.Press(VirtualKeyShort.F9);
            }

            log("Czekam na załadowanie zakładek w Opcjach...");
            AktywnySleep(2500, token, pauseEvent);

            Window oknoOpcji = SzukajOknaOpcje(app, automation);
            var oknoDoSzukania = oknoOpcji ?? mainWindow;

            log("Szukam pola 'Szukaj zakładki...' po współrzędnych przestrzennych...");
            AutomationElement poleSzukajZakladki = null;

            var wszystkieEdity = UiaSafeCall(() => oknoDoSzukania.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)), UiaPollTimeout, Array.Empty<AutomationElement>());

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
                log($"Zlokalizowano lewe pole wyszukiwania. Klikam...");
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
                    var rect = oknoDoSzukania.BoundingRectangle;
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
            var znalezioneZakladki = UiaSafeCall(() => oknoDoSzukania.FindAllDescendants(cf => cf.ByName("System praw")), UiaCallTimeout, Array.Empty<AutomationElement>());

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
            return true;
        }

        private static string OdczytajObecnySystemPraw(Window window, CancellationToken token, ManualResetEventSlim pauseEvent)
        {
            string stan = "";
            for (int k = 0; k < 6; k++)
            {
                pauseEvent.Wait(token);

                var comboboxy = UiaSafeCall(() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox)), Array.Empty<AutomationElement>());
                foreach (var cb in comboboxy)
                {
                    try
                    {
                        var comboEl = cb.AsComboBox();
                        string txt = comboEl.SelectedItem?.Text ?? "";
                        if (string.IsNullOrEmpty(txt) && comboEl.Patterns.Value.IsSupported)
                            txt = comboEl.Patterns.Value.Pattern.Value.Value ?? "";

                        if (!string.IsNullOrWhiteSpace(txt) && (txt.Contains("Standardowy") || txt.Contains("Rozszerzony")))
                        {
                            stan = txt.Trim();
                            break;
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(stan)) break;
                AktywnySleep(300, token, pauseEvent);
            }
            return stan;
        }

        private static void ZapiszIZamknijOpcjeI_Czekaj(Window mainWindow, FlaUI.Core.Application app, UIA3Automation automation, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log)
        {
            log("Zatwierdzam zapisywanie opcji (Zapisz i zamknij)...");

            Window oknoOpcji = SzukajOknaOpcje(app, automation);
            AutomationElement btnZapisz = null;

            var okna = PobierzWszystkieOkna(app, automation);
            foreach (var wnd in okna)
            {
                btnZapisz = UiaSafeCall(() => wnd.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij")));
                if (btnZapisz != null) break;
            }

            bool kliknieto = false;
            if (btnZapisz != null)
            {
                log(" -> Znaleziono przycisk 'Zapisz i zamknij'. Klikam...");
                try
                {
                    var inv = btnZapisz.Patterns.Invoke.PatternOrDefault;
                    if (inv != null)
                    {
                        inv.Invoke();
                        kliknieto = true;
                    }
                }
                catch { }

                if (!kliknieto)
                {
                    try
                    {
                        btnZapisz.AsButton().Click();
                        kliknieto = true;
                    }
                    catch { }
                }
            }

            if (!kliknieto)
            {
                log(" -> Wysyłam skrót Alt + Z...");
                if (oknoOpcji != null) oknoOpcji.Focus();
                else mainWindow.Focus();
                AktywnySleep(300, token, pauseEvent);
                using (Keyboard.Pressing(VirtualKeyShort.ALT)) { Keyboard.Press(VirtualKeyShort.KEY_Z); }
            }

            log("Czekam na przetworzenie transakcji i całkowite zamknięcie okna Opcji (DataForm)...");
            int brakOknaLicznik = 0;
            int maxCzekaniaSec = 60;

            for (int odczekanoSec = 0; odczekanoSec < maxCzekaniaSec; odczekanoSec++)
            {
                token.ThrowIfCancellationRequested();
                pauseEvent.Wait(token);
                AktywnySleep(1000, token, pauseEvent);

                var oknaObecne = PobierzWszystkieOkna(app, automation);
                var oknoStop = oknaObecne.FirstOrDefault(w => { try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")); } catch { return false; } });
                if (oknoStop != null)
                {
                    log(" -> Zauważono komunikat ostrzegawczy/błędu. Zamykam go klikając OK...");
                    try
                    {
                        var btnOk = oknoStop.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                        if (btnOk != null) btnOk.Click();
                        else { oknoStop.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                    }
                    catch { }
                    AktywnySleep(1000, token, pauseEvent);
                }

                Window opcjeNadalWisi = SzukajOknaOpcje(app, automation);
                if (opcjeNadalWisi == null)
                {
                    brakOknaLicznik++;
                    if (brakOknaLicznik >= 2)
                    {
                        log("Okno Opcje zostało pomyślnie zamknięte i zapisane.");
                        return;
                    }
                }
                else
                {
                    brakOknaLicznik = 0;

                    if (odczekanoSec > 0 && odczekanoSec % 6 == 0)
                    {
                        log(" -> Okno Opcje nadal otwarte, ponawiam sygnał Zapisz i zamknij...");
                        try
                        {
                            var btn = UiaSafeCall(() => opcjeNadalWisi.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton());
                            if (btn != null) btn.Click();
                            else
                            {
                                opcjeNadalWisi.Focus();
                                using (Keyboard.Pressing(VirtualKeyShort.ALT)) { Keyboard.Press(VirtualKeyShort.KEY_Z); }
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        private static void CzekajNaZmianeNaRozszerzony(Window window, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log)
        {
            log("Trwa przeliczanie ról (monituję zmianę statusu na 'Rozszerzony')...");
            int maksymalnyCzasSec = 180;

            for (int i = 0; i < maksymalnyCzasSec; i++)
            {
                token.ThrowIfCancellationRequested();
                pauseEvent.Wait(token);

                string stan = OdczytajObecnySystemPraw(window, token, pauseEvent);
                if (stan.Equals("Rozszerzony", StringComparison.OrdinalIgnoreCase))
                {
                    log("Wykryto zmianę systemu praw na ROZSZERZONY!");
                    break;
                }

                AktywnySleep(1000, token, pauseEvent);
            }

            AktywnySleep(2000, token, pauseEvent);
        }
    }
}