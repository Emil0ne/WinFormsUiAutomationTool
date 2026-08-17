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
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Automatyczne_Klawisze
{
    public class EnovaAktualizacja
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;

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

        private static List<Window> PobierzWszystkieOkna(FlaUI.Core.Application app, UIA3Automation automation)
        {
            var wynik = new List<Window>();
            if (app == null) return wynik;

            try
            {
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
                            if (modale != null) wynik.AddRange(modale);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return wynik;
        }

        private static readonly TimeSpan UiaCallTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UiaPollTimeout = TimeSpan.FromSeconds(2);

        public static void Uruchom(List<string> listaBaz, string login, string haslo, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log, Action<string> onBazaZakonczona = null)
        {
            log($"Rozpoczynam proces AKTUALIZACJI / KONWERSJI dla {listaBaz.Count} baz...");
            string plikRaportu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Raport_AktualizacjaBazy_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            try
            {
                string naglowek = $"RAPORT AKTUALIZACJI BAZ\nData rozpoczęcia: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nLiczba baz: {listaBaz.Count}\n==================================================\n";
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
                    log($"---> ROZPOCZYNAM AKTUALIZACJĘ: {nazwaBazy} <---");
                    log($"==========================================");

                    string powodBledu = "";
                    bool sukces = PrzetworzBazeAktualizacja(nazwaBazy, login, haslo, sciezkaEnova, token, pauseEvent, log, out powodBledu);

                    if (!sukces)
                    {
                        linijkaRaportu = $"{nazwaBazy} - BŁĄD: {powodBledu}";
                        log($"❌ BAZA '{nazwaBazy}' ZAKOŃCZONA BŁĘDEM: {powodBledu}");
                    }
                    else
                    {
                        linijkaRaportu = $"{nazwaBazy} - SUKCES";
                        log($"✅ BAZA '{nazwaBazy}' ZAKONWERTOWANA POMYŚLNIE.");
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
            log($"🏁 ZAKOŃCZONO PROCES AKTUALIZACJI.");
        }

        private static bool PrzetworzBazeAktualizacja(string nazwaBazy, string login, string haslo, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log, out string powodBledu)
        {
            powodBledu = "";
            Process startedProcess = null;
            FlaUI.Core.Application app = null;

            try
            {
                var processInfo = new ProcessStartInfo(sciezkaEnova) { UseShellExecute = true };
                startedProcess = Process.Start(processInfo);
                log("Uruchomiono nową instancję Enova365.");
                AktywnySleep(6000, token, pauseEvent);

                using (var automation = new UIA3Automation())
                {
                    automation.ConnectionTimeout = TimeSpan.FromSeconds(8);
                    automation.TransactionTimeout = TimeSpan.FromSeconds(8);

                    if (startedProcess == null) { powodBledu = "Nie udało się wystartować procesu."; return false; }
                    app = FlaUI.Core.Application.Attach(startedProcess);

                    log("Szukam pola wyboru bazy...");
                    Window mainWindow = null;
                    FlaUI.Core.AutomationElements.TextBox poleWyszukiwania = null;

                    for (int i = 0; i < 20; i++)
                    {
                        pauseEvent.Wait(token);
                        var okna = PobierzWszystkieOkna(app, automation);
                        mainWindow = okna.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } }) ?? okna.FirstOrDefault();
                        if (mainWindow == null) { AktywnySleep(500, token, pauseEvent); continue; }

                        var wszystkiePolaEdit = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)), UiaPollTimeout, Array.Empty<AutomationElement>());
                        foreach (var pole in wszystkiePolaEdit)
                        {
                            try { var textBox = pole.AsTextBox(); if ((textBox.Name != null && textBox.Name.Contains("Szukaj")) || (textBox.HelpText != null && textBox.HelpText.Contains("Szukaj"))) { poleWyszukiwania = textBox; break; } }
                            catch (System.Runtime.InteropServices.COMException) { }
                        }
                        if (poleWyszukiwania == null && wszystkiePolaEdit.Length > 0) { try { poleWyszukiwania = wszystkiePolaEdit[0].AsTextBox(); } catch { } }
                        if (poleWyszukiwania != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null || poleWyszukiwania == null) { powodBledu = "Nie udało się pobrać okna bazy lub pola wyszukiwania."; return false; }

                    poleWyszukiwania.Focus(); AktywnySleep(500, token, pauseEvent);
                    poleWyszukiwania.Text = $"\"{nazwaBazy}\"";
                    log($"Filtruję: {nazwaBazy}");
                    AktywnySleep(1500, token, pauseEvent);

                    var localMainWindow2 = mainWindow;
                    var znalezioneElementy = UiaSafeCall(() => localMainWindow2.FindAllDescendants(cf => cf.ByName(nazwaBazy)), UiaCallTimeout, Array.Empty<AutomationElement>());
                    AutomationElement elementBazy = znalezioneElementy.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Text) ?? znalezioneElementy.FirstOrDefault();

                    if (elementBazy != null)
                    {
                        log("Zlokalizowano bazę. Klikam...");
                        try { elementBazy.Click(); AktywnySleep(200, token, pauseEvent); elementBazy.DoubleClick(); } catch { }
                    }
                    else { powodBledu = "Nie znaleziono bazy."; return false; }

                    AktywnySleep(3000, token, pauseEvent);

                    // AKTUALIZACJA DODATKÓW
                    var modalne1 = UiaSafeCall(() => mainWindow.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                    var oknoAktualizacji = modalne1.FirstOrDefault(m => m.Name != null && m.Name.Contains("Aktualizacja dodatków"));
                    if (oknoAktualizacji != null)
                    {
                        log("Wykryto okno aktualizacji dodatków! Klikam 'Tak'...");
                        DateTime czasKlikniecia = DateTime.Now.AddSeconds(-2);
                        var btnTak = oknoAktualizacji.FindFirstDescendant(cf => cf.ByName("Tak"))?.AsButton();
                        if (btnTak != null) btnTak.Click(); else { oknoAktualizacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }

                        log("Czekam na całkowity reset (do 30 s)...");
                        int staryPid = startedProcess.Id;
                        string nazwaProcesu = System.IO.Path.GetFileNameWithoutExtension(sciezkaEnova);
                        Process nowyProces = null;
                        int odczekanoMs = 0;
                        while (odczekanoMs < 30000)
                        {
                            AktywnySleep(1000, token, pauseEvent); odczekanoMs += 1000;
                            var procesyTmp = Process.GetProcessesByName(nazwaProcesu);
                            nowyProces = procesyTmp.Where(p => p.Id != staryPid).OrderByDescending(p => { try { return p.StartTime; } catch { return DateTime.MinValue; } }).FirstOrDefault();
                            if (nowyProces != null) { try { if (nowyProces.StartTime >= czasKlikniecia) break; else nowyProces = null; } catch { break; } }
                        }

                        if (nowyProces != null)
                        {
                            startedProcess = nowyProces;
                            try { startedProcess.WaitForInputIdle(15000); } catch { }
                            app = FlaUI.Core.Application.Attach(startedProcess);
                        }
                        else { powodBledu = "Enova nie wstała po aktualizacji dodatków."; return false; }
                    }

                    // ==========================================
                    // KROK 2: LOGOWANIE
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
                        oknoLogowania.Focus(); AktywnySleep(500, token, pauseEvent);
                        Keyboard.Type(login); AktywnySleep(300, token, pauseEvent);
                        Keyboard.Press(VirtualKeyShort.TAB); AktywnySleep(300, token, pauseEvent);
                        if (!string.IsNullOrEmpty(haslo)) { Keyboard.Type(haslo); AktywnySleep(300, token, pauseEvent); }

                        var btnOk = oknoLogowania.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                        if (btnOk != null) btnOk.Click(); else Keyboard.Press(VirtualKeyShort.ENTER);

                        log("Zatwierdzono logowanie. Sprawdzam stan bazy...");
                        AktywnySleep(3000, token, pauseEvent);

                        bool zlyLogin = false;
                        bool nowszaWersja = false;
                        string wersjaNowszejBazy = "";
                        Window konwersjaWindow = null;

                        for (int i = 0; i < 40; i++)
                        {
                            pauseEvent.Wait(token);
                            var topWindows = PobierzWszystkieOkna(app, automation);

                            bool odrazuLicencje = topWindows.Any(w => { try { return w.Name != null && (w.Name.Contains("Pobrane licencje") || w.Name.Contains("Licencja programu")); } catch { return false; } });
                            if (odrazuLicencje)
                            {
                                log("Wykryto okno licencji. Baza nie wymaga konwersji!");
                                break;
                            }

                            konwersjaWindow = topWindows.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("Konwersja bazy"); } catch { return false; } });
                            Window errorWindow = topWindows.FirstOrDefault(w => { try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")); } catch { return false; } });

                            if (konwersjaWindow != null)
                            {
                                log("Wykryto okno 'Konwersja bazy'!");
                                break;
                            }

                            if (errorWindow != null)
                            {
                                string errorText = "";
                                try
                                {
                                    var textElements = errorWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                                    foreach (var te in textElements) { if (!string.IsNullOrWhiteSpace(te.Name)) errorText += te.Name + " "; }
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

                        if (nowszaWersja || zlyLogin)
                        {
                            try
                            {
                                var btnAnulujLogowanie = oknoLogowania?.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                btnAnulujLogowanie?.Click();
                            }
                            catch { }

                            if (nowszaWersja) { powodBledu = string.IsNullOrEmpty(wersjaNowszejBazy) ? "Baza pochodzi z nowszej wersji programu." : $"Baza pochodzi z nowszej wersji programu ({wersjaNowszejBazy})."; return false; }
                            if (zlyLogin) { powodBledu = "Odrzucono logowanie (Błędne hasło lub zablokowane konto)."; return false; }
                        }

                        // ==========================================
                        // KROK 3: JEŚLI JEST OKNO KONWERSJI -> KONWERTUJEMY
                        // ==========================================
                        if (konwersjaWindow != null)
                        {
                            log("Przetwarzam okno 'Konwersja bazy'...");
                            log("Zatwierdzam konwersję (szukam buttonOK w podoknie konwersji)...");

                            try
                            {
                                FlaUI.Core.AutomationElements.Button btnOkKonwersja = null;
                                for (int p = 0; p < 10; p++)
                                {
                                    btnOkKonwersja = UiaSafeCall(() => konwersjaWindow.FindFirstDescendant(cf => cf.ByAutomationId("buttonOK"))?.AsButton(), UiaPollTimeout)
                                                  ?? UiaSafeCall(() => konwersjaWindow.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton(), UiaPollTimeout);

                                    if (btnOkKonwersja != null) break;
                                    AktywnySleep(300, token, pauseEvent);
                                }

                                if (btnOkKonwersja != null)
                                {
                                    log(" -> Znalazłem buttonOK. Przekazuję fokus i wywołuję zdarzenie...");

                                    try { btnOkKonwersja.Focus(); AktywnySleep(300, token, pauseEvent); } catch { }

                                    try
                                    {
                                        IntPtr hwnd = btnOkKonwersja.Properties.NativeWindowHandle.ValueOrDefault;
                                        if (hwnd != IntPtr.Zero)
                                        {
                                            PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)1, IntPtr.Zero);
                                            AktywnySleep(100, token, pauseEvent);
                                            PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
                                            log(" -> Wysłano sygnał WM_LBUTTONDOWN/UP (PostMessage).");
                                        }
                                    }
                                    catch { }

                                    try { Keyboard.Press(VirtualKeyShort.SPACE); log(" -> Wciśnięto SPACJĘ na zogniskowanym przycisku."); } catch { }

                                    try
                                    {
                                        var inv = btnOkKonwersja.Patterns.Invoke.PatternOrDefault;
                                        if (inv != null) { inv.Invoke(); log(" -> Wywołano UIA InvokePattern."); }
                                    }
                                    catch { }
                                }
                                else
                                {
                                    log("BŁĄD: Nie znaleziono buttonOK w oknie konwersji.");
                                }
                            }
                            catch (Exception ex)
                            {
                                log($"Wyjątek przycisku OK: {ex.Message}");
                            }

                            log("Rozpoczęto konwersję bazy. To może potrwać kilka minut...");
                            int maxCzasKonwersjiMs = 600000; // 10 minut limitu
                            int czasKonwersjiMs = 0;
                            bool konwersjaZakonczona = false;

                            while (czasKonwersjiMs < maxCzasKonwersjiMs)
                            {
                                pauseEvent.Wait(token);
                                AktywnySleep(3000, token, pauseEvent);
                                czasKonwersjiMs += 3000;

                                var aktualneOkna = PobierzWszystkieOkna(app, automation);

                                if (aktualneOkna == null || aktualneOkna.Count == 0)
                                {
                                    if (startedProcess.HasExited)
                                    {
                                        powodBledu = "Proces Enovy niespodziewanie się zakończył.";
                                        break;
                                    }
                                    continue;
                                }

                                bool licencjeWisi = aktualneOkna.Any(w => {
                                    try
                                    {
                                        string n = w.Name ?? "";
                                        return n.Contains("Pobrane licencje") || n.Contains("Licencja programu") || n.Contains("Informacja") || n.Contains("Zapisz i zamknij");
                                    }
                                    catch { return false; }
                                });

                                if (licencjeWisi)
                                {
                                    log("Wykryto okno licencji/informacji. Konwersja zakończona!");
                                    konwersjaZakonczona = true;
                                    break;
                                }

                                bool bladWisi = aktualneOkna.Any(w => {
                                    try
                                    {
                                        string n = w.Name ?? "";
                                        return n.Contains("Stop") || n.Contains("Błąd") || n.Contains("Raport błędu");
                                    }
                                    catch { return false; }
                                });

                                if (bladWisi)
                                {
                                    powodBledu = "Wystąpił błąd Enovy podczas trwania konwersji.";
                                    konwersjaZakonczona = false;
                                    break;
                                }

                                if (czasKonwersjiMs % 60000 == 0) { log($"(info) Konwersja w toku... ({czasKonwersjiMs / 60000} min)"); }
                            }

                            if (!konwersjaZakonczona)
                            {
                                if (string.IsNullOrEmpty(powodBledu)) powodBledu = "Przekroczono czas oczekiwania na konwersję.";
                                return false;
                            }
                        }
                        else
                        {
                            log("Baza jest już w docelowej wersji (pomijam konwersję).");
                        }
                    }
                    else { powodBledu = "Brak okna logowania."; return false; }

                    // ==========================================
                    // KROK 4: OBSŁUGA LICENCJI I ZAMKNIĘCIE BAZY
                    // ==========================================
                    log("Sprawdzam okno licencji / powiadomień po starcie...");
                    for (int j = 0; j < 40; j++)
                    {
                        pauseEvent.Wait(token);
                        var wszystkieOknaPo = PobierzWszystkieOkna(app, automation);

                        // 1. Wykrycie okna 'Licencja programu'
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
                                var przyciski = UiaSafeCall(() => oknoLicencjaProg.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)), UiaPollTimeout, Array.Empty<AutomationElement>());
                                var btn = przyciski.FirstOrDefault(b => { try { return b.Name != null && b.Name.Contains("zainstalowaną"); } catch { return false; } });
                                if (btn != null) btn.Click();
                                else { oknoLicencjaProg.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            }
                            AktywnySleep(1500, token, pauseEvent);

                            // Czekamy na okno 'Wybierz licencję'
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
                            log("Znaleziono ekran licencji ('Pobrane licencje'). Klikam 'Zapisz i zamknij'...");
                            oknoLicencji.Focus(); AktywnySleep(500, token, pauseEvent);
                            if (btnOdznacz != null) { try { if (btnOdznacz.IsEnabled) { var ip = btnOdznacz.Patterns.Invoke.PatternOrDefault; if (ip != null) ip.Invoke(); else btnOdznacz.Click(); } } catch { } }
                            try { var ip = btnZapisz.Patterns.Invoke.PatternOrDefault; if (ip != null) ip.Invoke(); else btnZapisz.Click(); } catch { }
                            AktywnySleep(2500, token, pauseEvent);
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

                    return true;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { powodBledu = $"Wyjątek systemu: {ex.Message}"; return false; }
            finally
            {
                try { app?.Close(); } catch { }
                try { if (startedProcess != null && !startedProcess.HasExited) startedProcess.Kill(); } catch { }
                AktywnySleep(1000, token, pauseEvent);
            }
        }
    }
}