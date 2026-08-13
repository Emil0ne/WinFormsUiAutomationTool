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
    public class EnovaKonwersjaPraw
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

        public static void Uruchom(List<string> listaBaz, string login, string haslo, string sciezkaEnova, bool tylkoUzgodnijRole, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log)
        {
            string trybText = tylkoUzgodnijRole ? "TYLKO UZGODNIENIE RÓL" : "PEŁNA KONWERSJA (ROZSZERZONY)";
            log($"Rozpoczynam proces konwersji systemu praw [{trybText}] dla {listaBaz.Count} baz...");
            string plikRaportu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Raport_KonwersjaPraw_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            List<string> wynikiRaporu = new List<string>();

            foreach (var nazwaBazy in listaBaz)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    pauseEvent.Wait(token);

                    log($"\n==========================================");
                    log($"---> KONWERSJA BAZY: {nazwaBazy} <---");
                    log($"==========================================");

                    string konwersjaWynik = "";
                    bool sukces = KonwertujSystemPrawDlaBazy(nazwaBazy, login, haslo, sciezkaEnova, tylkoUzgodnijRole, token, pauseEvent, log, out konwersjaWynik);

                    if (!sukces)
                    {
                        wynikiRaporu.Add($"{nazwaBazy} - BŁĄD: {konwersjaWynik}");
                        log($"❌ BAZA '{nazwaBazy}' ZAKOŃCZONA BŁĘDEM: {konwersjaWynik}");
                    }
                    else
                    {
                        wynikiRaporu.Add($"{nazwaBazy} - {konwersjaWynik}");
                        log($"✅ BAZA '{nazwaBazy}' -> Status: {konwersjaWynik}");
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
            log($"🏁 ZAKOŃCZONO PROCES KONWERSJI SYSTEMU PRAW.");

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

        private static bool KonwertujSystemPrawDlaBazy(string nazwaBazy, string login, string haslo, string sciezkaEnova, bool tylkoUzgodnijRole, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log, out string wynik)
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

                    // Aktualizacja dodatków
                    var oknaMod = PobierzWszystkieOkna(app, automation);
                    var oknoAktualizacji = oknaMod.FirstOrDefault(m => m.Name != null && m.Name.Contains("Aktualizacja dodatków"));
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

                        log("Zatwierdzono logowanie. Czekam na weryfikację...");

                        bool logowanieNieudane = false;
                        string bladLogowania = "";

                        for (int i = 0; i < 20; i++)
                        {
                            pauseEvent.Wait(token);
                            var topW = PobierzWszystkieOkna(app, automation);

                            Window oknoBledu = topW.FirstOrDefault(w => {
                                try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd") || w.Name.Contains("Konwersja")); }
                                catch { return false; }
                            });

                            if (oknoBledu != null)
                            {
                                logowanieNieudane = true;
                                bladLogowania = "Odrzucono logowanie (Błędny login, hasło lub zablokowane konto).";
                                log("❌ Wykryto błąd logowania! Zamykam komunikaty...");

                                try { oknoBledu.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton()?.Click(); } catch { }
                                AktywnySleep(1000, token, pauseEvent);
                                try { oknoLogowania.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton()?.Click(); } catch { }
                                break;
                            }

                            bool logowanieNadalWidoczne = topW.Any(w => {
                                try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); }
                                catch { return false; }
                            });

                            if (!logowanieNadalWidoczne) break;
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
                        var wszystkieOkna = PobierzWszystkieOkna(app, automation);

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
                            if (modale.Length == 0 && j >= 4) break;
                        }

                        AktywnySleep(800, token, pauseEvent);
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

                    if (mainWindow == null) { wynik = "Nie udało się pobrać głównego okna bazy."; return false; }

                    // Otwieramy zakładkę "System praw"
                    if (!OtworzZakladkeSystemPraw(mainWindow, app, automation, token, pauseEvent, log))
                    {
                        wynik = "Nie udało się otworzyć zakładki System praw.";
                        return false;
                    }

                    // Pobieramy okno Opcje
                    Window oknoOpcji = SzukajOknaOpcje(app, automation);
                    var oknoRobocze = oknoOpcji ?? mainWindow;

                    // 1. CZY SYSTEM JEST JUŻ ROZSZERZONY?
                    string sprawdzonyStan = OdczytajObecnySystemPraw(oknoRobocze, token, pauseEvent);
                    if (sprawdzonyStan.Equals("Rozszerzony", StringComparison.OrdinalIgnoreCase))
                    {
                        log("ℹ️ Baza posiada już system ROZSZERZONY. Pomijam Dalsze akcje.");
                        wynik = "Pomięto (Baza miała już system rozszerzony)";

                        try { (oknoOpcji ?? mainWindow).Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); AktywnySleep(800, token, pauseEvent); } catch { }
                        return true;
                    }

                    // 2. PRZYCISK "Uzgodnij standardowe role" (JEŚLI WYSTĘPUJE)
                    var btnUzgodnij = UiaSafeCall(() => oknoRobocze.FindFirstDescendant(cf => cf.ByName("Uzgodnij standardowe role"))?.AsButton(), UiaCallTimeout);
                    bool wykonanoUzgodnienie = false;

                    if (btnUzgodnij != null && btnUzgodnij.IsEnabled)
                    {
                        log("Wykryto konieczność wykonania 'Uzgodnij standardowe role'. Klikam...");
                        btnUzgodnij.Click();
                        AktywnySleep(2000, token, pauseEvent);

                        var oknaWszystkie = PobierzWszystkieOkna(app, automation);
                        var oknoUzgodnijModal = oknaWszystkie.FirstOrDefault(w => w != oknoRobocze && w != mainWindow);
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

                    // ----------------------------------------------------
                    // WARUNEK DLA OPCJI 1: TYLKO UZGODNIENIE RÓL
                    // ----------------------------------------------------
                    if (tylkoUzgodnijRole)
                    {
                        if (wykonanoUzgodnienie)
                        {
                            wynik = "Pomyślnie wykonano uzgodnienie standardowych ról.";
                            log($"✅ Sukces: {wynik}");
                            return true;
                        }
                        else
                        {
                            log("ℹ️ Standardowe role były już wcześniej uzgodnione. Zamykam opcje...");
                            try { (oknoOpcji ?? mainWindow).Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); AktywnySleep(800, token, pauseEvent); } catch { }
                            wynik = "Role były już wcześniej uzgodnione.";
                            return true;
                        }
                    }

                    // ----------------------------------------------------
                    // OPCJA 2: PEŁNA KONWERSJA DALSZA CZĘŚĆ
                    // ----------------------------------------------------
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

                    // 3. PRZYCISK "Zmień system praw na rozszerzony"
                    var btnZmienPrawa = UiaSafeCall(() => oknoRobocze.FindFirstDescendant(cf => cf.ByName("Zmień system praw na rozszerzony"))?.AsButton(), UiaCallTimeout);
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
                                var btn = UiaSafeCall(() => wnd.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton(), UiaPollTimeout);
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
                try { app?.Close(); } catch { }
                try { if (startedProcess != null && !startedProcess.HasExited) startedProcess.WaitForExit(3000); } catch { }
                try { if (startedProcess != null && !startedProcess.HasExited) startedProcess.Kill(); } catch { }
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
                try { return w.Name != null && w.Name.Equals("Opcje", StringComparison.OrdinalIgnoreCase); }
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

            var wszystkieEdity = UiaSafeCall(() => oknoDoSzukania.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)), UiaCallTimeout, Array.Empty<AutomationElement>());

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
                    var rect = oknoDoSzukania.BoundingRectangle;
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
            var znalezioneZakladki = UiaSafeCall(() => oknoDoSzukania.FindAllDescendants(cf => cf.ByName("System praw")), UiaCallTimeout, Array.Empty<AutomationElement>());

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
            return true;
        }

        private static string OdczytajObecnySystemPraw(Window window, CancellationToken token, ManualResetEventSlim pauseEvent)
        {
            string stan = "";
            for (int k = 0; k < 6; k++)
            {
                pauseEvent.Wait(token);

                var comboboxy = UiaSafeCall(() => window.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox)), UiaPollTimeout, Array.Empty<AutomationElement>());
                foreach (var cb in comboboxy)
                {
                    try
                    {
                        var comboEl = cb.AsComboBox();
                        string txt = comboEl.SelectedItem?.Text ?? "";
                        if (string.IsNullOrEmpty(txt) && comboEl.Patterns.Value.IsSupported) txt = comboEl.Patterns.Value.Pattern.Value.Value ?? "";
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
            var targetWnd = oknoOpcji ?? mainWindow;

            var btnZapisz = UiaSafeCall(() => targetWnd.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton(), UiaCallTimeout);
            if (btnZapisz != null)
            {
                btnZapisz.Click();
            }
            else
            {
                targetWnd.Focus();
                using (Keyboard.Pressing(VirtualKeyShort.ALT)) { Keyboard.Press(VirtualKeyShort.KEY_Z); }
            }

            log("Czekam na przetworzenie transakcji i całkowite zamknięcie okna Opcji...");
            int maxCzekaniaSec = 60;
            int odczekanoSec = 0;

            while (odczekanoSec < maxCzekaniaSec)
            {
                token.ThrowIfCancellationRequested();
                pauseEvent.Wait(token);
                AktywnySleep(1000, token, pauseEvent);
                odczekanoSec++;

                Window opcjeNadalWisi = SzukajOknaOpcje(app, automation);
                if (opcjeNadalWisi == null)
                {
                    log("Okno Opcje zostało pomyślnie zamknięte i zapisane.");
                    break;
                }

                // Jeżeli po 5 sekundach okno nadal wisi, próbuje dobić je skrótem Alt + Z
                if (odczekanoSec % 5 == 0)
                {
                    log(" -> Okno Opcje nadal otwarte, ponawiam sygnał Zapisz i zamknij (Alt + Z)...");
                    try
                    {
                        opcjeNadalWisi.Focus();
                        using (Keyboard.Pressing(VirtualKeyShort.ALT)) { Keyboard.Press(VirtualKeyShort.KEY_Z); }
                    }
                    catch { }
                }
            }

            AktywnySleep(2000, token, pauseEvent);
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