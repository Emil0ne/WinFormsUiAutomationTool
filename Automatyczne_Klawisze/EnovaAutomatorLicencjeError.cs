using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Automatyczne_Klawisze
{
    public class EnovaAutomatorLicencjeError
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
                    if (startedProcess == null)
                    {
                        powodBledu = "Nie udało się wystartować procesu Enovy.";
                        return false;
                    }

                    app = FlaUI.Core.Application.Attach(startedProcess);
                    Window mainWindow = null;

                    // BEZPIECZNE POBIERANIE OKNA (omija deadlock GetMainWindow)
                    for (int i = 0; i < 20; i++)
                    {
                        mainWindow = app.GetAllTopLevelWindows(automation).FirstOrDefault();
                        if (mainWindow != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (mainWindow == null)
                    {
                        powodBledu = "Główne okno Enovy nie pojawiło się po starcie.";
                        return false;
                    }

                    // ==========================================
                    // KROK 1: WYSZUKIWANIE I ODPALANIE BAZY
                    // ==========================================
                    log("Szukam pola wyboru bazy...");
                    FlaUI.Core.AutomationElements.TextBox poleWyszukiwania = null;
                    var wszystkiePolaEdit = mainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));

                    foreach (var pole in wszystkiePolaEdit)
                    {
                        pauseEvent.Wait(token);
                        var textBox = pole.AsTextBox();
                        if ((textBox.Name != null && textBox.Name.Contains("Szukaj")) ||
                            (textBox.HelpText != null && textBox.HelpText.Contains("Szukaj")))
                        {
                            poleWyszukiwania = textBox;
                            break;
                        }
                    }

                    if (poleWyszukiwania == null && wszystkiePolaEdit.Length > 0)
                        poleWyszukiwania = wszystkiePolaEdit[0].AsTextBox();

                    if (poleWyszukiwania != null)
                    {
                        string szukanaFraza = $"\"{nazwaBazy}\"";
                        poleWyszukiwania.Focus();
                        AktywnySleep(500, token, pauseEvent);

                        poleWyszukiwania.Text = szukanaFraza;
                        log($"Filtruję listę dla: {szukanaFraza}");
                        AktywnySleep(1500, token, pauseEvent);

                        var znalezioneElementy = mainWindow.FindAllDescendants(cf => cf.ByName(nazwaBazy));
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
                            try
                            {
                                foreach (var modal in mainWindow.ModalWindows)
                                {
                                    if (modal.Name.Contains("Aktualizacja dodatków"))
                                    {
                                        oknoAktualizacji = modal;
                                        break;
                                    }
                                }
                            }
                            catch { }

                            if (oknoAktualizacji != null)
                            {
                                log("Wykryto okno aktualizacji! Klikam 'Tak'...");
                                var btnTak = oknoAktualizacji.FindFirstDescendant(cf => cf.ByName("Tak"))?.AsButton();
                                if (btnTak != null) btnTak.Click();
                                else { oknoAktualizacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }

                                log("Czekam 15 sekund na całkowity reset Enovy...");
                                int staryPid = startedProcess.Id;
                                AktywnySleep(15000, token, pauseEvent);

                                string nazwaProcesu = System.IO.Path.GetFileNameWithoutExtension(sciezkaEnova);
                                var procesy = Process.GetProcessesByName(nazwaProcesu);

                                // Wyciągamy nowy proces, by bot nie dusił się na starym ID
                                var nowyProces = procesy.FirstOrDefault(p => p.Id != staryPid) ?? procesy.FirstOrDefault();

                                if (nowyProces != null)
                                {
                                    startedProcess = nowyProces;
                                    app = FlaUI.Core.Application.Attach(startedProcess);
                                    log($"Ponownie podpięto się pod proces Enovy (PID: {startedProcess.Id}).");
                                }
                                else
                                {
                                    powodBledu = "Enova nie wstała ponownie po aktualizacji dodatków.";
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
                        try
                        {
                            var topWindows = app.GetAllTopLevelWindows(automation);
                            foreach (var wnd in topWindows)
                            {
                                if (wnd.Name != null && wnd.Name.Contains("Logowanie do bazy")) { oknoLogowania = wnd; break; }
                                foreach (var modal in wnd.ModalWindows)
                                {
                                    if (modal.Name != null && modal.Name.Contains("Logowanie do bazy")) { oknoLogowania = modal; break; }
                                }
                                if (oknoLogowania != null) break;
                            }
                        }
                        catch { }

                        if (oknoLogowania != null) break;
                        AktywnySleep(500, token, pauseEvent);
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

                        // Dajemy bazie bezpieczny bufor startowy (3 sekundy), żeby w ogóle zdążyła rzucić błąd lub zacząć mielić
                        AktywnySleep(3000, token, pauseEvent);

                        bool zlyLogin = false;
                        bool wymagaKonwersji = false;

                        // Zwiększamy liczbę prób do 24 (12 sekund), żeby wolne bazy po aktualizacji nie oszukały bota
                        for (int i = 0; i < 24; i++)
                        {
                            pauseEvent.Wait(token);

                            Window errorWindow = null;
                            Window konwersjaWindow = null;

                            try
                            {
                                konwersjaWindow = oknoLogowania.ModalWindows.FirstOrDefault(m => m.Name != null && m.Name.Contains("Konwersja bazy"));
                                errorWindow = oknoLogowania.ModalWindows.FirstOrDefault(m => m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd")));

                                if (errorWindow == null)
                                {
                                    foreach (var m in oknoLogowania.ModalWindows)
                                    {
                                        if (m.FindFirstDescendant(cf => cf.ByName("Raport błędu")) != null) { errorWindow = m; break; }
                                    }
                                }
                            }
                            catch { }

                            if (konwersjaWindow == null && errorWindow == null)
                            {
                                try
                                {
                                    var topWindows = app.GetAllTopLevelWindows(automation);
                                    konwersjaWindow = topWindows.FirstOrDefault(w => w.Name != null && w.Name.Contains("Konwersja bazy"));
                                    errorWindow = topWindows.FirstOrDefault(w => w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")));

                                    // Sprawdzamy czy okno logowania nadal istnieje
                                    bool logowanieIstnieje = topWindows.Any(w => w.Name != null && w.Name.Contains("Logowanie do bazy"));
                                    if (!logowanieIstnieje)
                                    {
                                        // Okno zniknęło – upewniamy się czy główne okno Enovy już wstało, zanim uciekniemy z pętli!
                                        var testMain = topWindows.FirstOrDefault(w => w.Name != null && w.Name.Contains("enova365"));
                                        if (testMain != null) break; // Enova gotowa!
                                    }
                                }
                                catch { }
                            }

                            if (konwersjaWindow != null)
                            {
                                wymagaKonwersji = true;
                                var btnAnulujKonwersje = konwersjaWindow.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                if (btnAnulujKonwersje != null) btnAnulujKonwersje.Click();
                                else { konwersjaWindow.Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); }
                                AktywnySleep(1000, token, pauseEvent);
                                break;
                            }

                            if (errorWindow != null)
                            {
                                zlyLogin = true;
                                var btnOkError = errorWindow.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                if (btnOkError != null) btnOkError.Click();
                                else { errorWindow.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                AktywnySleep(1000, token, pauseEvent);
                                break;
                            }

                            if (wymagaKonwersji || zlyLogin) break;
                            AktywnySleep(500, token, pauseEvent);
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

                        log("Logowanie powiodło się. Oczekiwanie na pełne wyrenderowanie okna licencji...");
                        AktywnySleep(2000, token, pauseEvent);

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

                        log("Logowanie poprawne. Przechodzę do weryfikacji licencji...");
                        AktywnySleep(1500, token, pauseEvent);
                    }
                    else
                    {
                        powodBledu = "Nie doczekano się na pojawienie okna logowania.";
                        return false;
                    }

                    // ==========================================
                    // KROK 3: LICENCJE
                    // ==========================================
                    log("Sprawdzam okno licencji...");
                    FlaUI.Core.AutomationElements.Button btnOdznacz = null;
                    Window oknoLicencji = null;

                    for (int j = 0; j < 20; j++)
                    {
                        pauseEvent.Wait(token);
                        try
                        {
                            var wszystkieOkna = app.GetAllTopLevelWindows(automation);
                            foreach (var wnd in wszystkieOkna)
                            {
                                btnOdznacz = wnd.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje"))?.AsButton();
                                if (btnOdznacz != null) { oknoLicencji = wnd; break; }
                            }
                        }
                        catch { }

                        if (btnOdznacz != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (btnOdznacz != null && oknoLicencji != null)
                    {
                        log("Znaleziono licencje. Odznaczam i zapisuję...");
                        try { if (btnOdznacz.IsEnabled) btnOdznacz.Click(); } catch { }
                        AktywnySleep(1500, token, pauseEvent);

                        var btnZapisz = oknoLicencji.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                        if (btnZapisz != null)
                        {
                            btnZapisz.Click();
                            AktywnySleep(3000, token, pauseEvent);
                        }
                    }

                    // POBIERAMY ZAKTUALIZOWANE OKNO GŁÓWNE (Zamiast blokującego GetMainWindow)
                    log("Pobieram główne okno po zalogowaniu...");
                    mainWindow = null;
                    for (int i = 0; i < 15; i++)
                    {
                        try
                        {
                            var okna = app.GetAllTopLevelWindows(automation);
                            mainWindow = okna.FirstOrDefault(w => w.Name != null && w.Name.Contains("enova365")) ?? okna.FirstOrDefault();
                            if (mainWindow != null) break;
                        }
                        catch { }
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
                        oknoOtwierania = mainWindow.ModalWindows.FirstOrDefault(m => m.Name != null && (m.Name.Contains("Otwieranie") || m.Name.Contains("Open")));
                        if (oknoOtwierania == null)
                        {
                            var topWindows = app.GetAllTopLevelWindows(automation);
                            oknoOtwierania = topWindows.FirstOrDefault(m => m.Name != null && (m.Name.Contains("Otwieranie") || m.Name.Contains("Open")));
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
                            oknoInformacji = mainWindow.ModalWindows.FirstOrDefault(m => m.Name != null && (m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja")));
                            if (oknoInformacji == null)
                            {
                                var topWindows = app.GetAllTopLevelWindows(automation);
                                oknoInformacji = topWindows.FirstOrDefault(m => m.Name != null && (m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja")));
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
                        var wszystkieElementy = mainWindow.FindAllDescendants();
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

                        var btnUstawHaslo = mainWindow.FindFirstDescendant(cf => cf.ByName("Ustaw hasło..."))?.AsButton()
                                            ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Ustaw hasło"))?.AsButton();

                        if (btnUstawHaslo != null)
                        {
                            btnUstawHaslo.Click();
                            Window oknoUstawiania = null;

                            for (int k = 0; k < 20; k++)
                            {
                                pauseEvent.Wait(token);
                                var wszystkieOkna = app.GetAllTopLevelWindows(automation);
                                foreach (var w in wszystkieOkna)
                                {
                                    if (!string.IsNullOrEmpty(w.Name) && (w.Name.IndexOf("hasł", StringComparison.OrdinalIgnoreCase) >= 0 || w.Name.IndexOf("Ustawien", StringComparison.OrdinalIgnoreCase) >= 0))
                                    { oknoUstawiania = w; break; }

                                    var btnBrak = w.FindFirstDescendant(cf => cf.ByName("Brak"))?.AsButton();
                                    if (btnBrak != null) { oknoUstawiania = w; break; }
                                }

                                if (oknoUstawiania == null)
                                {
                                    foreach (var m in mainWindow.ModalWindows)
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

                                var btnZapiszKoncowe = mainWindow.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                                if (btnZapiszKoncowe != null)
                                {
                                    btnZapiszKoncowe.Click();
                                    AktywnySleep(2000, token, pauseEvent);
                                    return true; // PEŁEN SUKCES
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
                // SPRZĄTANIE
                try { app?.Close(); } catch { }
                try { if (startedProcess != null && !startedProcess.HasExited) startedProcess.Kill(); } catch { }
                AktywnySleep(1000, token, pauseEvent);
            }
        }
    }
}