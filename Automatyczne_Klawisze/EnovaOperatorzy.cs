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
    public class EnovaOperatorzy
    {
        // Elastyczne usypianie reagujące na Stop i Pauzę
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

        // =======================================================
        // BEZPIECZNY WRAPPER NA WYWOŁANIA UIA/COM
        // =======================================================
        private static T UiaSafeCall<T>(Func<T> action, TimeSpan timeout, T fallback = default)
        {
            try
            {
                var task = Task.Run(action);
                if (task.Wait(timeout))
                {
                    return task.IsFaulted ? fallback : task.Result;
                }
                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static readonly TimeSpan UiaCallTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UiaPollTimeout = TimeSpan.FromSeconds(2);

        // =======================================================
        // POMOCNICZA METODA DO ZAPISU LOGÓW DO PLIKU TXT
        // =======================================================
        public static void ZapiszLogiDoPliku(IEnumerable<string> linieLogow, Action<string> log)
        {
            try
            {
                string sciezkaPliku = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Logi_Enova_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllLines(sciezkaPliku, linieLogow);
                log($"📁 Pomyślnie wyeksportowano logi do pliku na Pulpicie: {Path.GetFileName(sciezkaPliku)}");
            }
            catch (Exception ex)
            {
                log($"❌ Błąd podczas zapisu logów do pliku: {ex.Message}");
            }
        }

        // =======================================================
        // GŁÓWNA FUNKCJA STERUJĄCA PĘTLĄ I RAPORTAMI
        // =======================================================
        public static void Uruchom(List<string> listaBaz, string login, string haslo, string nowyOperator, string hasloOperatora, string sciezkaXml, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log)
        {
            log($"Uruchamianie automatyzacji dla {listaBaz.Count} baz...");

            string plikRaportu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Raport_Bledow_Enova_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            List<string> bledneBazy = new List<string>();

            foreach (var nazwaBazy in listaBaz)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    pauseEvent.Wait(token);

                    log($"\n==========================================");
                    log($"---> ROZPOCZYNAM TEST BAZY: {nazwaBazy} <---");
                    log($"==========================================");

                    string powodBledu = "";
                    bool sukces = PrzetworzBaze(nazwaBazy, login, haslo, nowyOperator, hasloOperatora, sciezkaXml, sciezkaEnova, token, pauseEvent, log, out powodBledu);

                    if (!sukces)
                    {
                        bledneBazy.Add($"- {nazwaBazy}: {powodBledu}");
                        log($"❌ BAZA '{nazwaBazy}' ZAKOŃCZONA BŁĘDEM: {powodBledu}");
                    }
                    else
                    {
                        log($"✅ BAZA '{nazwaBazy}' PRZETWORZONA POMYŚLNIE.");
                    }
                }
                catch (OperationCanceledException)
                {
                    log("\n🛑 AUTOMATYZACJA ZOSTAŁA PRZERWANA NA ŻĄDANIE UŻYTKOWNIKA.");
                    break;
                }
                catch (Exception ex)
                {
                    log($"BŁĄD KRYTYCZNY PĘTLI: {ex.Message}");
                }
            }

            log($"\n==========================================");
            log($"🏁 ZAKOŃCZONO PRZETWARZANIE WSZYSTKICH BAZ.");

            if (bledneBazy.Count > 0)
            {
                log($"UWAGA: Wystąpiły błędy w {bledneBazy.Count} bazach.");
                try
                {
                    File.WriteAllLines(plikRaportu, bledneBazy);
                    log($"Zapisano plik z raportem błędów na Pulpicie: {Path.GetFileName(plikRaportu)}");
                }
                catch (Exception ex)
                {
                    log($"Nie udało się zapisać pliku z raportem: {ex.Message}");
                }
            }
            else
            {
                log("🎉 WSZYSTKIE BAZY PRZETWORZONE BEZ ANI JEDNEGO BŁĘDU!");
            }
        }

        // =======================================================
        // FUNKCJA PRZETWARZAJĄCA POJEDYNCZĄ BAZĘ
        // =======================================================
        private static bool PrzetworzBaze(string nazwaBazy, string login, string haslo, string nowyOperator, string hasloOperatora, string sciezkaXml, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log, out string powodBledu)
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

                    if (startedProcess == null)
                    {
                        powodBledu = "Nie udało się wystartować procesu Enovy.";
                        return false;
                    }

                    app = FlaUI.Core.Application.Attach(startedProcess);

                    // ==========================================
                    // BEZPIECZNE POBIERANIE OKNA I KROK 1 (SZUKANIE BAZY)
                    // ==========================================
                    log("Szukam pola wyboru bazy (i stabilizuję okno główne)...");
                    Window mainWindow = null;
                    FlaUI.Core.AutomationElements.TextBox poleWyszukiwania = null;

                    for (int i = 0; i < 20; i++)
                    {
                        pauseEvent.Wait(token);

                        var localApp = app;
                        var okna = UiaSafeCall(() => localApp.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());

                        mainWindow = okna.FirstOrDefault(w => {
                            try { return w.Name != null && w.Name.Contains("enova365"); }
                            catch { return false; }
                        }) ?? okna.FirstOrDefault();

                        if (mainWindow == null)
                        {
                            AktywnySleep(500, token, pauseEvent);
                            continue;
                        }

                        var wszystkiePolaEdit = UiaSafeCall(
                            () => mainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)),
                            UiaPollTimeout, Array.Empty<AutomationElement>());

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
                            catch (System.Runtime.InteropServices.COMException) { }
                        }

                        if (poleWyszukiwania == null && wszystkiePolaEdit.Length > 0)
                        {
                            try { poleWyszukiwania = wszystkiePolaEdit[0].AsTextBox(); }
                            catch { }
                        }

                        if (poleWyszukiwania != null) break;

                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null)
                    {
                        powodBledu = "Główne okno Enovy nie ustabilizowało się po starcie.";
                        return false;
                    }

                    if (poleWyszukiwania != null)
                    {
                        string szukanaFraza = $"\"{nazwaBazy}\"";
                        poleWyszukiwania.Focus();
                        AktywnySleep(500, token, pauseEvent);

                        poleWyszukiwania.Text = szukanaFraza;
                        log($"Filtruję listę dla: {szukanaFraza}");
                        AktywnySleep(1500, token, pauseEvent);

                        var localMainWindow2 = mainWindow;
                        var znalezioneElementy = UiaSafeCall(
                            () => localMainWindow2.FindAllDescendants(cf => cf.ByName(nazwaBazy)),
                            UiaCallTimeout, Array.Empty<AutomationElement>());
                        AutomationElement elementBazy = null;

                        if (znalezioneElementy.Length > 0)
                        {
                            elementBazy = znalezioneElementy.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Text);
                            if (elementBazy == null) elementBazy = znalezioneElementy[0];
                        }

                        if (elementBazy != null)
                        {
                            log($"Zlokalizowano bazę. Wykonuję kliknięcie...");
                            try
                            {
                                elementBazy.Click();
                                AktywnySleep(200, token, pauseEvent);
                                elementBazy.DoubleClick();
                            }
                            catch { }

                            // --- OBSŁUGA OKNA AKTUALIZACJI DODATKÓW ---
                            AktywnySleep(3000, token, pauseEvent);
                            Window oknoAktualizacji = null;
                            var localMainWindow3 = mainWindow;
                            var modalne1 = UiaSafeCall(() => localMainWindow3.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                            foreach (var modal in modalne1)
                            {
                                if (modal.Name != null && modal.Name.Contains("Aktualizacja dodatków"))
                                {
                                    oknoAktualizacji = modal;
                                    break;
                                }
                            }

                            if (oknoAktualizacji != null)
                            {
                                log("Wykryto okno aktualizacji! Klikam 'Tak'...");

                                // KLUCZOWE: Zapisujemy datę sprzed kliknięcia, aby zignorować procesy z tła
                                DateTime czasKlikniecia = DateTime.Now.AddSeconds(-2);

                                var btnTak = oknoAktualizacji.FindFirstDescendant(cf => cf.ByName("Tak"))?.AsButton();
                                if (btnTak != null) btnTak.Click();
                                else { oknoAktualizacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }

                                log("Czekam na całkowity reset Enovy (do 30 sekund)...");
                                int staryPid = startedProcess.Id;
                                string nazwaProcesu = System.IO.Path.GetFileNameWithoutExtension(sciezkaEnova);

                                Process nowyProces = null;
                                int maxCzekaniaMs = 30000;
                                int odczekanoMs = 0;

                                while (odczekanoMs < maxCzekaniaMs)
                                {
                                    AktywnySleep(1000, token, pauseEvent); // Sprawdzamy co 1 sekundę
                                    odczekanoMs += 1000;

                                    var procesyTmp = Process.GetProcessesByName(nazwaProcesu);

                                    // Szukamy najnowszego procesu ignorując stary
                                    nowyProces = procesyTmp
                                        .Where(p => p.Id != staryPid)
                                        .OrderByDescending(p => { try { return p.StartTime; } catch { return DateTime.MinValue; } })
                                        .FirstOrDefault();

                                    if (nowyProces != null)
                                    {
                                        try
                                        {
                                            // Upewniamy się, że to nowa instancja, a nie stara porzucona w Menedżerze Zadań
                                            if (nowyProces.StartTime >= czasKlikniecia)
                                            {
                                                break; // Znaleźliśmy poprawny, nowy proces!
                                            }
                                            else
                                            {
                                                // Znaleziono proces z tła. Odrzucamy go na razie i czekamy na ten właściwy
                                                nowyProces = null;
                                            }
                                        }
                                        catch
                                        {
                                            // Fallback dla braku praw odczytu czasu (rzadki przypadek)
                                            break;
                                        }
                                    }
                                }

                                if (nowyProces != null)
                                {
                                    startedProcess = nowyProces;

                                    try { startedProcess.WaitForInputIdle(15000); }
                                    catch (Exception exIdle) { log($"(info) WaitForInputIdle: {exIdle.Message}"); }

                                    app = FlaUI.Core.Application.Attach(startedProcess);
                                    log($"Ponownie podpięto się pod zaktualizowany proces Enovy (PID: {startedProcess.Id}).");

                                    mainWindow = null;
                                }
                                else
                                {
                                    powodBledu = "Enova nie wstała ponownie po aktualizacji dodatków w czasie 30 sekund.";
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            powodBledu = "Nie znaleziono bazy na liście po przefiltrowaniu.";
                            return false;
                        }
                    }
                    else
                    {
                        powodBledu = "Nie znaleziono pola wyszukiwania (SearchBox).";
                        return false;
                    }

                    // ==========================================
                    // KROK 2: LOGOWANIE
                    // ==========================================
                    log("Oczekuję na okno logowania...");
                    Window oknoLogowania = null;

                    for (int i = 0; i < 20; i++)
                    {
                        pauseEvent.Wait(token);
                        var localApp2 = app;
                        var topWindows = UiaSafeCall(() => localApp2.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());
                        oknoLogowania = topWindows.FirstOrDefault(w => {
                            try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); }
                            catch { return false; }
                        });

                        if (oknoLogowania == null)
                        {
                            foreach (var wnd in topWindows)
                            {
                                var localWnd = wnd;
                                var modale = UiaSafeCall(() => localWnd.ModalWindows, UiaPollTimeout, Array.Empty<Window>());
                                var znalezione = modale.FirstOrDefault(m => {
                                    try { return m.Name != null && m.Name.Contains("Logowanie do bazy"); }
                                    catch { return false; }
                                });
                                if (znalezione != null) { oknoLogowania = znalezione; break; }
                            }
                        }

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
                        if (btnOk != null) btnOk.Click();
                        else Keyboard.Press(VirtualKeyShort.ENTER);

                        log("Zatwierdzono logowanie. Czekam na załadowanie bazy po aktualizacji...");

                        AktywnySleep(3000, token, pauseEvent);

                        bool zlyLogin = false;
                        bool wymagaKonwersji = false;
                        bool sukcesPotwierdzony = false;

                        for (int i = 0; i < 40; i++)
                        {
                            pauseEvent.Wait(token);

                            Window errorWindow = null;
                            Window konwersjaWindow = null;

                            var localApp3 = app;
                            var topWindows = UiaSafeCall(() => localApp3.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());
                            konwersjaWindow = topWindows.FirstOrDefault(w => {
                                try { return w.Name != null && w.Name.Contains("Konwersja bazy"); } catch { return false; }
                            });
                            errorWindow = topWindows.FirstOrDefault(w => {
                                try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")); } catch { return false; }
                            });

                            if (errorWindow == null && konwersjaWindow == null)
                            {
                                var localOknoLogowania = oknoLogowania;
                                var modaleLog = UiaSafeCall(() => localOknoLogowania.ModalWindows, UiaPollTimeout, Array.Empty<Window>());
                                konwersjaWindow = modaleLog.FirstOrDefault(m => {
                                    try { return m.Name != null && m.Name.Contains("Konwersja bazy"); } catch { return false; }
                                });
                                errorWindow = modaleLog.FirstOrDefault(m => {
                                    try { return m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd")); } catch { return false; }
                                });

                                if (errorWindow == null && konwersjaWindow == null && i % 5 == 0)
                                {
                                    foreach (var m in modaleLog)
                                    {
                                        var localM = m;
                                        var raport = UiaSafeCall(() => localM.FindFirstDescendant(cf => cf.ByName("Raport błędu")), UiaPollTimeout);
                                        if (raport != null) { errorWindow = m; break; }
                                    }
                                }
                            }

                            if (konwersjaWindow == null && errorWindow == null)
                            {
                                bool logowanieIstnieje = topWindows.Any(w => {
                                    try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); } catch { return false; }
                                });

                                // Jeśli okno logowania zniknęło i nie ma błędu – to znaczy, że się powiodło!
                                if (!logowanieIstnieje)
                                {
                                    sukcesPotwierdzony = true;
                                    break;
                                }
                            }

                            if (konwersjaWindow != null)
                            {
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

                            if (errorWindow != null)
                            {
                                zlyLogin = true;
                                try
                                {
                                    var btnOkError = errorWindow.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                    if (btnOkError != null) btnOkError.Click();
                                    else { errorWindow.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                }
                                catch { }
                                AktywnySleep(1000, token, pauseEvent);
                                break;
                            }

                            if (wymagaKonwersji || zlyLogin) break;
                            AktywnySleep(300, token, pauseEvent);
                        }

                        if (!wymagaKonwersji && !zlyLogin && !sukcesPotwierdzony)
                        {
                            log("⚠ Brak jednoznacznego potwierdzenia zalogowania - wykonuję dokładniejszą kontrolę końcową...");

                            var localApp3b = app;
                            var topWindowsFinal = UiaSafeCall(() => localApp3b.GetAllTopLevelWindows(automation), UiaCallTimeout, Array.Empty<Window>());
                            var errorFinal = topWindowsFinal.FirstOrDefault(w => {
                                try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")); } catch { return false; }
                            });

                            if (errorFinal == null)
                            {
                                var localOknoLogowaniaB = oknoLogowania;
                                var modaleLogB = UiaSafeCall(() => localOknoLogowaniaB.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                                errorFinal = modaleLogB.FirstOrDefault(m => {
                                    try { return m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd")); } catch { return false; }
                                });
                            }

                            if (errorFinal != null)
                            {
                                zlyLogin = true;
                                try
                                {
                                    var btnOkErrorFinal = errorFinal.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                    if (btnOkErrorFinal != null) btnOkErrorFinal.Click();
                                    else { errorFinal.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                }
                                catch { }
                                AktywnySleep(1000, token, pauseEvent);
                            }
                            else
                            {
                                bool logowanieIstniejeFinal = topWindowsFinal.Any(w => {
                                    try { return w.Name != null && w.Name.Contains("Logowanie do bazy"); } catch { return false; }
                                });
                                if (!logowanieIstniejeFinal)
                                {
                                    sukcesPotwierdzony = true;
                                }
                            }
                        }

                        if (wymagaKonwersji)
                        {
                            powodBledu = "Baza wymaga konwersji (zbyt stara wersja).";
                            return false;
                        }

                        if (zlyLogin)
                        {
                            powodBledu = "Odrzucono logowanie (Błędne hasło lub zablokowane konto).";
                            return false;
                        }

                        if (!sukcesPotwierdzony)
                        {
                            powodBledu = "Nie udało się jednoznacznie potwierdzić poprawnego zalogowania w wyznaczonym czasie (możliwy niewykryty błąd logowania lub bardzo wolne ładowanie bazy).";
                            return false;
                        }

                        log("Logowanie poprawne. Przechodzę do weryfikacji licencji...");
                    }
                    else
                    {
                        powodBledu = "Nie doczekano się na pojawienie okna logowania.";
                        return false;
                    }

                    // ==========================================
                    // KROK 3: LICENCJE
                    // ==========================================
                    log("Czekam na ewentualne okno licencji (szukam przycisków)...");
                    Window oknoLicencji = null;
                    AutomationElement btnOdznacz = null;
                    AutomationElement btnZapisz = null;

                    // Dajemy Enovie 7.5 sekundy (15 prób x 500ms) na pokazanie okna licencji
                    for (int j = 0; j < 15; j++)
                    {
                        pauseEvent.Wait(token);

                        var localApp4 = app;
                        var wszystkieOkna = UiaSafeCall(() => localApp4.GetAllTopLevelWindows(automation), UiaPollTimeout, Array.Empty<Window>());

                        foreach (var wnd in wszystkieOkna)
                        {
                            var localWnd = wnd;
                            // Szukamy okna po obecności przycisku Zapisz
                            btnZapisz = UiaSafeCall(() => localWnd.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij")), UiaPollTimeout);

                            if (btnZapisz != null)
                            {
                                oknoLicencji = wnd;
                                btnOdznacz = UiaSafeCall(() => localWnd.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje")), UiaPollTimeout);
                                break;
                            }
                        }

                        // Jeśli znaleźliśmy przycisk zapisu, nie ma sensu czekać dłużej
                        if (btnZapisz != null) break;

                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (oknoLicencji != null && btnZapisz != null)
                    {
                        log("Znaleziono ekran licencji. Próbuję odznaczyć i zapisać...");
                        oknoLicencji.Focus();
                        AktywnySleep(500, token, pauseEvent);

                        if (btnOdznacz != null)
                        {
                            try
                            {
                                if (btnOdznacz.IsEnabled)
                                {
                                    // Używamy patternu wywołania, bezpieczniejsze dla Ribbonów
                                    var invPattern = btnOdznacz.Patterns.Invoke.PatternOrDefault;
                                    if (invPattern != null) invPattern.Invoke();
                                    else btnOdznacz.Click();

                                    log("Kliknięto 'Odznacz niedostępne licencje'.");
                                    AktywnySleep(1000, token, pauseEvent);
                                }
                                else
                                {
                                    log("(info) Przycisk 'Odznacz niedostępne licencje' jest zablokowany - pomijam.");
                                }
                            }
                            catch { }
                        }

                        log("Klikam 'Zapisz i zamknij'...");
                        try
                        {
                            var invPattern = btnZapisz.Patterns.Invoke.PatternOrDefault;
                            if (invPattern != null) invPattern.Invoke();
                            else btnZapisz.Click();
                        }
                        catch { }

                        // Solidne opóźnienie po zamknięciu licencji, by UI wróciło do normy przed importem XML
                        AktywnySleep(3500, token, pauseEvent);
                    }
                    else
                    {
                        log("Nie wykryto licencji w wyznaczonym czasie. Zakładam, że baza załadowała się bezpośrednio.");
                    }

                    log("Pobieram główne okno po zalogowaniu...");
                    mainWindow = null;
                    for (int i = 0; i < 15; i++)
                    {
                        var localApp5 = app;
                        var okna = UiaSafeCall(() => localApp5.GetAllTopLevelWindows(automation), UiaCallTimeout, Array.Empty<Window>());
                        mainWindow = okna.FirstOrDefault(w => {
                            try { return w.Name != null && w.Name.Contains("enova365"); } catch { return false; }
                        }) ?? okna.FirstOrDefault();
                        if (mainWindow != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null)
                    {
                        powodBledu = "Nie udało się pobrać okna bazy po logowaniu.";
                        return false;
                    }

                    // ==========================================
                    // KROK 4: IMPORT XML
                    // ==========================================
                    log("Otwieram import XML...");
                    mainWindow.Focus();
                    AktywnySleep(500, token, pauseEvent);

                    using (Keyboard.Pressing(VirtualKeyShort.ALT)) { Keyboard.Press(VirtualKeyShort.KEY_P); }
                    AktywnySleep(600, token, pauseEvent);
                    Keyboard.Press(VirtualKeyShort.KEY_I);
                    AktywnySleep(600, token, pauseEvent);
                    Keyboard.Press(VirtualKeyShort.KEY_Z);
                    AktywnySleep(1500, token, pauseEvent);

                    Window oknoOtwierania = null;
                    for (int i = 0; i < 20; i++)
                    {
                        pauseEvent.Wait(token);

                        var localMainWindow4 = mainWindow;
                        var modaleOtw = UiaSafeCall(() => localMainWindow4.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                        oknoOtwierania = modaleOtw.FirstOrDefault(m => {
                            try { return m.Name != null && (m.Name.Contains("Otwieranie") || m.Name.Contains("Open")); } catch { return false; }
                        });

                        Window oknoBleduUprawnien = modaleOtw.FirstOrDefault(m => {
                            try { return m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd")); } catch { return false; }
                        });

                        Window[] topWindows = Array.Empty<Window>();
                        if (oknoOtwierania == null || oknoBleduUprawnien == null)
                        {
                            var localApp6 = app;
                            topWindows = UiaSafeCall(() => localApp6.GetAllTopLevelWindows(automation), UiaCallTimeout, Array.Empty<Window>());

                            if (oknoOtwierania == null)
                                oknoOtwierania = topWindows.FirstOrDefault(m => {
                                    try { return m.Name != null && (m.Name.Contains("Otwieranie") || m.Name.Contains("Open")); } catch { return false; }
                                });

                            if (oknoBleduUprawnien == null)
                                oknoBleduUprawnien = topWindows.FirstOrDefault(w => {
                                    try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")); } catch { return false; }
                                });
                        }

                        if (oknoBleduUprawnien != null)
                        {
                            log("❌ Wykryto brak uprawnień przy próbie importu XML.");
                            try
                            {
                                var btnOkUprawnienia = oknoBleduUprawnien.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                if (btnOkUprawnienia != null) btnOkUprawnienia.Click();
                                else { oknoBleduUprawnien.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                            }
                            catch { }
                            AktywnySleep(1000, token, pauseEvent);

                            powodBledu = "Brak uprawnień do importu XML (konto operatora ma inny system praw).";
                            return false;
                        }

                        if (oknoOtwierania != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (oknoOtwierania != null)
                    {
                        oknoOtwierania.Focus();
                        AktywnySleep(1000, token, pauseEvent);
                        Keyboard.Type(sciezkaXml);
                        AktywnySleep(1000, token, pauseEvent);
                        Keyboard.Press(VirtualKeyShort.ENTER);

                        Window oknoInformacji = null;
                        for (int i = 0; i < 20; i++)
                        {
                            pauseEvent.Wait(token);

                            var localMainWindow5 = mainWindow;
                            var modaleInfo = UiaSafeCall(() => localMainWindow5.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                            oknoInformacji = modaleInfo.FirstOrDefault(m => {
                                try { return m.Name != null && (m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja")); } catch { return false; }
                            });
                            Window oknoBleduUprawnien2 = modaleInfo.FirstOrDefault(m => {
                                try { return m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd")); } catch { return false; }
                            });

                            if (oknoInformacji == null || oknoBleduUprawnien2 == null)
                            {
                                var localApp7 = app;
                                var topWindows = UiaSafeCall(() => localApp7.GetAllTopLevelWindows(automation), UiaCallTimeout, Array.Empty<Window>());
                                if (oknoInformacji == null)
                                    oknoInformacji = topWindows.FirstOrDefault(m => {
                                        try { return m.Name != null && (m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja")); } catch { return false; }
                                    });
                                if (oknoBleduUprawnien2 == null)
                                    oknoBleduUprawnien2 = topWindows.FirstOrDefault(w => {
                                        try { return w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")); } catch { return false; }
                                    });
                            }

                            if (oknoBleduUprawnien2 != null)
                            {
                                log("❌ Wykryto brak uprawnień po wskazaniu pliku XML do importu.");
                                try
                                {
                                    var btnOkUprawnienia2 = oknoBleduUprawnien2.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                    if (btnOkUprawnienia2 != null) btnOkUprawnienia2.Click();
                                    else { oknoBleduUprawnien2.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                }
                                catch { }
                                AktywnySleep(1000, token, pauseEvent);

                                powodBledu = "Brak uprawnień do importu XML (konto operatora ma inny system praw).";
                                return false;
                            }

                            if (oknoInformacji != null) break;
                            AktywnySleep(500, token, pauseEvent);
                        }

                        if (oknoInformacji != null)
                        {
                            var btnOkInfo = oknoInformacji.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                            if (btnOkInfo != null) btnOkInfo.Click();
                            else { oknoInformacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                        }
                    }

                    // ==========================================
                    // KROK 5: ZMIANA HASŁA OPERATORA
                    // ==========================================
                    AktywnySleep(2000, token, pauseEvent);
                    log("Wyszukuję operatora na liście...");

                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) { Keyboard.Press(VirtualKeyShort.F9); }
                    AktywnySleep(1500, token, pauseEvent);
                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) { Keyboard.Press(VirtualKeyShort.KEY_O); }
                    AktywnySleep(3000, token, pauseEvent);

                    AutomationElement wpisOperatora = null;
                    for (int i = 0; i < 5; i++)
                    {
                        pauseEvent.Wait(token);

                        var localMainWindow6 = mainWindow;
                        var wszystkieElementy = UiaSafeCall(() => localMainWindow6.FindAllDescendants(), UiaCallTimeout, Array.Empty<AutomationElement>());
                        foreach (var el in wszystkieElementy)
                        {
                            try
                            {
                                string nazwaElementu = el.Name;
                                if (!string.IsNullOrWhiteSpace(nazwaElementu))
                                {
                                    if (nazwaElementu.Trim().Equals(nowyOperator.Trim(), StringComparison.OrdinalIgnoreCase)) { wpisOperatora = el; break; }
                                    if (el.ControlType == FlaUI.Core.Definitions.ControlType.DataItem && nazwaElementu.Contains(nowyOperator)) { wpisOperatora = el; break; }
                                }
                                if (el.Patterns.Value.IsSupported)
                                {
                                    string wartosc = el.Patterns.Value.Pattern.Value.Value;
                                    if (!string.IsNullOrWhiteSpace(wartosc) && wartosc.Trim().Equals(nowyOperator.Trim(), StringComparison.OrdinalIgnoreCase)) { wpisOperatora = el; break; }
                                }
                            }
                            catch { }
                        }
                        if (wpisOperatora != null) break;
                        AktywnySleep(1000, token, pauseEvent);
                    }

                    if (wpisOperatora != null)
                    {
                        wpisOperatora.Click();
                        AktywnySleep(1000, token, pauseEvent);

                        var localMainWindow7 = mainWindow;
                        var btnUstawHaslo = UiaSafeCall(() => localMainWindow7.FindFirstDescendant(cf => cf.ByName("Ustaw hasło..."))?.AsButton(), UiaCallTimeout)
                                            ?? UiaSafeCall(() => localMainWindow7.FindFirstDescendant(cf => cf.ByName("Ustaw hasło"))?.AsButton(), UiaCallTimeout);

                        if (btnUstawHaslo != null)
                        {
                            btnUstawHaslo.Click();
                            Window oknoUstawiania = null;

                            for (int k = 0; k < 20; k++)
                            {
                                pauseEvent.Wait(token);

                                var localApp8 = app;
                                var wszystkieOkna = UiaSafeCall(() => localApp8.GetAllTopLevelWindows(automation), UiaCallTimeout, Array.Empty<Window>());
                                foreach (var w in wszystkieOkna)
                                {
                                    if (!string.IsNullOrEmpty(w.Name) && (w.Name.IndexOf("hasł", StringComparison.OrdinalIgnoreCase) >= 0 || w.Name.IndexOf("Ustawien", StringComparison.OrdinalIgnoreCase) >= 0))
                                    { oknoUstawiania = w; break; }

                                    var localW = w;
                                    var btnBrak = UiaSafeCall(() => localW.FindFirstDescendant(cf => cf.ByName("Brak"))?.AsButton(), UiaCallTimeout);
                                    if (btnBrak != null) { oknoUstawiania = w; break; }
                                }

                                if (oknoUstawiania == null)
                                {
                                    var localMainWindow8 = mainWindow;
                                    var modaleUst = UiaSafeCall(() => localMainWindow8.ModalWindows, UiaCallTimeout, Array.Empty<Window>());
                                    foreach (var m in modaleUst)
                                    {
                                        if (!string.IsNullOrEmpty(m.Name) && (m.Name.IndexOf("hasł", StringComparison.OrdinalIgnoreCase) >= 0))
                                        { oknoUstawiania = m; break; }
                                    }
                                }

                                if (oknoUstawiania != null) break;
                                AktywnySleep(500, token, pauseEvent);
                            }

                            if (oknoUstawiania != null)
                            {
                                oknoUstawiania.Focus();
                                AktywnySleep(1000, token, pauseEvent);

                                var pierwszePole = oknoUstawiania.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
                                if (pierwszePole != null)
                                {
                                    pierwszePole.Click();
                                    AktywnySleep(300, token, pauseEvent);
                                }

                                Keyboard.Type(hasloOperatora);
                                AktywnySleep(400, token, pauseEvent);
                                Keyboard.Press(VirtualKeyShort.TAB);
                                AktywnySleep(400, token, pauseEvent);
                                Keyboard.Type(hasloOperatora);
                                AktywnySleep(400, token, pauseEvent);

                                var btnOkHaslo = oknoUstawiania.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                if (btnOkHaslo != null) { btnOkHaslo.Click(); }
                                else { Keyboard.Press(VirtualKeyShort.ENTER); }

                                AktywnySleep(2500, token, pauseEvent);

                                var localMainWindow9 = mainWindow;
                                var btnZapiszKoncowe = UiaSafeCall(() => localMainWindow9.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton(), UiaCallTimeout);
                                if (btnZapiszKoncowe != null)
                                {
                                    btnZapiszKoncowe.Click();
                                    AktywnySleep(2000, token, pauseEvent);
                                    return true;
                                }
                                else
                                {
                                    powodBledu = "Nie znaleziono przycisku 'Zapisz i zamknij' na koniec.";
                                    return false;
                                }
                            }
                            else
                            {
                                powodBledu = "Okno wpisywania hasła nie pojawiło się.";
                                return false;
                            }
                        }
                        else
                        {
                            powodBledu = "Brak przycisku 'Ustaw hasło...' na pasku operatorów.";
                            return false;
                        }
                    }
                    else
                    {
                        powodBledu = "Operator nie pojawił się na liście (błąd importu XML lub złe ID).";
                        return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                powodBledu = $"Nieoczekiwany błąd systemu: {ex.Message}";
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