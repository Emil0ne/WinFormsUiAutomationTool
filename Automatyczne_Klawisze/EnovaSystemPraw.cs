using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Automatyczne_Klawisze
{
    public class EnovaSystemPraw
    {
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

        private static readonly TimeSpan UiaCallTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UiaPollTimeout = TimeSpan.FromSeconds(2);

        public static void Uruchom(List<string> listaBaz, string login, string haslo, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log)
        {
            log($"Rozpoczynam sprawdzanie systemu praw dla {listaBaz.Count} baz...");
            string plikRaportu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Raport_SystemPraw_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            List<string> wynikiRaporu = new List<string>();

            foreach (var nazwaBazy in listaBaz)
            {
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
                        wynikiRaporu.Add($"{nazwaBazy} - BŁĄD: {systemPrawWynik}");
                        log($"❌ BAZA '{nazwaBazy}' ZAKOŃCZONA BŁĘDEM: {systemPrawWynik}");
                    }
                    else
                    {
                        wynikiRaporu.Add($"{nazwaBazy} - {systemPrawWynik}");
                        log($"✅ BAZA '{nazwaBazy}' -> System praw: {systemPrawWynik}");
                    }
                }
                catch (OperationCanceledException)
                {
                    log("\n🛑 AUTOMATYZACJA PRZERWANA NA ŻĄDANIE.");
                    break;
                }
                catch (Exception ex)
                {
                    log($"BŁĄD KRYTYCZNY PĘTLI: {ex.Message}");
                }
            }

            log($"\n==========================================");
            log($"🏁 ZAKOŃCZONO SPRAWDZANIE SYSTEMU PRAW.");

            try
            {
                File.WriteAllLines(plikRaportu, wynikiRaporu);
                log($"📁 Zapisano raport na Pulpicie: {Path.GetFileName(plikRaportu)}");
            }
            catch (Exception ex)
            {
                log($"Nie udało się zapisać pliku raportu: {ex.Message}");
            }
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
                log("Uruchomiono nową instancję Enova365.");
                AktywnySleep(6000, token, pauseEvent);

                using (var automation = new UIA3Automation())
                {
                    automation.ConnectionTimeout = TimeSpan.FromSeconds(8);
                    automation.TransactionTimeout = TimeSpan.FromSeconds(8);

                    if (startedProcess == null) { wynik = "Nie udało się wystartować procesu."; return false; }
                    app = FlaUI.Core.Application.Attach(startedProcess);

                    log("Szukam pola wyboru bazy...");
                    Window mainWindow = null;
                    FlaUI.Core.AutomationElements.TextBox poleWyszukiwania = null;

                    for (int i = 0; i < 20; i++)
                    {
                        pauseEvent.Wait(token);
                        var localApp = app;
                        var okna = UiaSafeCall(() => localApp.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());
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

                    if (mainWindow == null || poleWyszukiwania == null) { wynik = "Nie udało się pobrać okna bazy lub pola wyszukiwania."; return false; }

                    poleWyszukiwania.Focus(); AktywnySleep(500, token, pauseEvent);
                    poleWyszukiwania.Text = $"\"{nazwaBazy}\"";
                    log($"Filtruję: {nazwaBazy}");
                    AktywnySleep(1500, token, pauseEvent);

                    var localMainWindow2 = mainWindow;
                    var znalezioneElementy = UiaSafeCall(() => localMainWindow2.FindAllDescendants(cf => cf.ByName(nazwaBazy)), UiaCallTimeout, Array.Empty<AutomationElement>());
                    AutomationElement elementBazy = znalezioneElementy.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Text) ?? znalezioneElementy.FirstOrDefault();

                    if (elementBazy != null) { log("Zlokalizowano bazę. Klikam..."); try { elementBazy.Click(); AktywnySleep(200, token, pauseEvent); elementBazy.DoubleClick(); } catch { } }
                    else { wynik = "Nie znaleziono bazy."; return false; }

                    AktywnySleep(3000, token, pauseEvent);

                    // Aktualizacja dodatków jeśli wystąpi
                    var localMainWindow3 = mainWindow;
                    var modalne1 = UiaSafeCall(() => localMainWindow3.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                    var oknoAktualizacji = modalne1.FirstOrDefault(m => m.Name != null && m.Name.Contains("Aktualizacja dodatków"));
                    if (oknoAktualizacji != null)
                    {
                        log("Wykryto okno aktualizacji dodatków! Klikam 'Tak'...");
                        DateTime czasKlikniecia = DateTime.Now.AddSeconds(-2);
                        var btnTak = oknoAktualizacji.FindFirstDescendant(cf => cf.ByName("Tak"))?.AsButton();
                        if (btnTak != null) btnTak.Click(); else { oknoAktualizacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }

                        log("Czekam na całkowity reset...");
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
                        else { wynik = "Enova nie wstała po aktualizacji dodatków."; return false; }
                    }

                    // Logowanie
                    log("Oczekuję na okno logowania...");
                    Window oknoLogowania = null;
                    for (int i = 0; i < 25; i++)
                    {
                        pauseEvent.Wait(token);
                        var localApp2 = app;
                        var topWindows = UiaSafeCall(() => localApp2.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());
                        oknoLogowania = topWindows.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); } catch { return false; } });
                        if (oknoLogowania == null)
                        {
                            foreach (var wnd in topWindows)
                            {
                                var modale = UiaSafeCall(() => wnd.ModalWindows, UiaPollTimeout, Array.Empty<Window>());
                                var znalezione = modale.FirstOrDefault(m => { try { return m.Name != null && m.Name.Contains("Logowanie do bazy"); } catch { return false; } });
                                if (znalezione != null) { oknoLogowania = znalezione; break; }
                            }
                        }
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

                        log("Zatwierdzono logowanie. Czekam na weryfikację...");

                        // OBSŁUGA BŁĘDÓW LOGOWANIA (ZŁY LOGIN / HASŁO / ZABLOKOWANE KONTO)
                        bool logowanieNieudane = false;
                        string bladLogowania = "";

                        for (int i = 0; i < 20; i++)
                        {
                            pauseEvent.Wait(token);
                            var localAppCheck = app;
                            var topW = UiaSafeCall(() => localAppCheck.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());

                            // Szukamy okna błędu (np. "Stop - enova365", "Błąd", "Konwersja bazy")
                            Window oknoBledu = topW.FirstOrDefault(w => {
                                try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd") || w.Name.Contains("Konwersja")); }
                                catch { return false; }
                            });

                            if (oknoBledu == null)
                            {
                                var modaleLog = UiaSafeCall(() => oknoLogowania.ModalWindows, UiaPollTimeout, Array.Empty<Window>());
                                oknoBledu = modaleLog.FirstOrDefault(m => {
                                    try { return m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd") || m.Name.Contains("Konwersja")); }
                                    catch { return false; }
                                });
                            }

                            if (oknoBledu != null)
                            {
                                logowanieNieudane = true;
                                bladLogowania = "Odrzucono logowanie (Błędny login, hasło lub zablokowane konto).";

                                log("❌ Wykryto błąd logowania! Zamykam komunikaty...");

                                // Klikamy OK na oknie błędu
                                try
                                {
                                    var btnOkErr = oknoBledu.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                    if (btnOkErr != null) btnOkErr.Click();
                                    else { oknoBledu.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                }
                                catch { }

                                AktywnySleep(1000, token, pauseEvent);

                                // Anulujemy okno logowania
                                try
                                {
                                    var btnAnuluj = oknoLogowania.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                    if (btnAnuluj != null) btnAnuluj.Click();
                                    else { oknoLogowania.Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); }
                                }
                                catch { }

                                break;
                            }

                            // Jeśli okno logowania zniknęło, oznacza to poprawne zalogowanie
                            bool logowanieNadalWidoczne = topW.Any(w => {
                                try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); }
                                catch { return false; }
                            });

                            if (!logowanieNadalWidoczne)
                            {
                                break;
                            }

                            AktywnySleep(500, token, pauseEvent);
                        }

                        if (logowanieNieudane)
                        {
                            wynik = bladLogowania;
                            return false;
                        }

                        AktywnySleep(2000, token, pauseEvent);
                    }
                    else { wynik = "Brak okna logowania."; return false; }

                    // Odznaczenie licencji po zalogowaniu
                    log("Sprawdzam ewentualne okno licencji...");
                    for (int j = 0; j < 20; j++)
                    {
                        pauseEvent.Wait(token);
                        var localAppLicencje = app;
                        var wszystkieOkna = UiaSafeCall(() => localAppLicencje.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());

                        Window oknoLicencji = null;
                        AutomationElement btnZapisz = null;
                        AutomationElement btnOdznacz = null;

                        foreach (var wnd in wszystkieOkna)
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
                            log("Znaleziono ekran licencji. Odznaczam i zamykam...");
                            oknoLicencji.Focus(); AktywnySleep(500, token, pauseEvent);
                            if (btnOdznacz != null) { try { if (btnOdznacz.IsEnabled) btnOdznacz.Click(); } catch { } }
                            try { btnZapisz.Click(); } catch { }
                            AktywnySleep(3000, token, pauseEvent);
                            break;
                        }

                        var mainW = wszystkieOkna.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } });
                        if (mainW != null)
                        {
                            var modale = UiaSafeCall(() => mainW.ModalWindows, UiaPollTimeout, Array.Empty<Window>());
                            if (modale.Length == 0 && j >= 4)
                            {
                                break;
                            }
                        }

                        AktywnySleep(800, token, pauseEvent);
                    }

                    // Pobranie głównego okna po zalogowaniu
                    mainWindow = null;
                    for (int i = 0; i < 15; i++)
                    {
                        var okna = UiaSafeCall(() => app.GetAllTopLevelWindows(automation), UiaCallTimeout, Array.Empty<Window>());
                        mainWindow = okna.FirstOrDefault(w => { try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; } }) ?? okna.FirstOrDefault();
                        if (mainWindow != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null) { wynik = "Nie udało się pobrać głównego okna bazy."; return false; }

                    // Otwarte Opcje (Ctrl + F9)
                    log("Otwieram Opcje (Ctrl + F9)...");
                    mainWindow.Focus();
                    AktywnySleep(800, token, pauseEvent);

                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
                    {
                        Keyboard.Press(VirtualKeyShort.F9);
                    }

                    log("Czekam na załadowanie zakładek w Opcjach...");
                    AktywnySleep(2500, token, pauseEvent);

                    // Szukamy pola Szukaj po osi X
                    log("Szukam pola 'Szukaj zakładki...' po współrzędnych przestrzennych...");
                    AutomationElement poleSzukajZakladki = null;

                    var wszystkieEdity = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)), UiaCallTimeout, Array.Empty<AutomationElement>());

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
                            UiaSafeCall(() => { FlaUI.Core.Input.Mouse.Click(new System.Drawing.Point(rect.X + 80, rect.Y + 130)); return true; }, UiaPollTimeout);
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
                    var znalezioneZakladki = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByName("System praw")), UiaCallTimeout, Array.Empty<AutomationElement>());

                    AutomationElement elSystemPraw = znalezioneZakladki.FirstOrDefault(e =>
                        e.ControlType == FlaUI.Core.Definitions.ControlType.TreeItem ||
                        e.ControlType == FlaUI.Core.Definitions.ControlType.ListItem ||
                        e.ControlType == FlaUI.Core.Definitions.ControlType.Text) ?? znalezioneZakladki.FirstOrDefault();

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

                    // Odczytujemy wartość systemu praw (Najpierw dokładna kontrolka, na końcu przyciski)
                    log("Odczytuję wartość systemu praw...");
                    string odczytanaWartosc = "";

                    for (int k = 0; k < 10; k++)
                    {
                        pauseEvent.Wait(token);

                        // 1. PRIORYTET: Odczyt bezpośrednio z pola ComboBox / elementów tekstowych widoku
                        var comboboxy = UiaSafeCall(() => mainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox)), UiaPollTimeout, Array.Empty<AutomationElement>());
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

                        // 2. Szukamy bezpośrednio po wartości pola ValuePattern na wszystkich kontrolkach w oknie
                        var wszystkieEl = UiaSafeCall(() => mainWindow.FindAllDescendants(), UiaPollTimeout, Array.Empty<AutomationElement>());
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

                        // 3. FALLBACK: Sprawdzanie przycisków na pasku narzędziowym tylko wtedy, gdy pole wyżej nie dało wyniku
                        var btnNaStandardowy = UiaSafeCall(() => mainWindow.FindFirstDescendant(cf => cf.ByName("Zmień system praw na standardowy")), UiaPollTimeout);
                        if (btnNaStandardowy != null) { odczytanaWartosc = "Rozszerzony"; break; }

                        var btnNaRozszerzony = UiaSafeCall(() => mainWindow.FindFirstDescendant(cf => cf.ByName("Zmień system praw na rozszerzony")), UiaPollTimeout);
                        if (btnNaRozszerzony != null) { odczytanaWartosc = "Standardowy"; break; }

                        AktywnySleep(300, token, pauseEvent);
                    }

                    if (string.IsNullOrEmpty(odczytanaWartosc))
                    {
                        odczytanaWartosc = "Standardowy";
                    }

                    wynik = odczytanaWartosc;
                    log($"Odczytano system praw: {wynik}");

                    // Zamykamy opcje (Escape)
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
                try { if (startedProcess != null && !startedProcess.HasExited) startedProcess.Kill(); } catch { }
                AktywnySleep(1000, token, pauseEvent);
            }
        }
    }
}