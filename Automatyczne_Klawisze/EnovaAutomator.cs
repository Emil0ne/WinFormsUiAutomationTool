using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Automatyczne_Klawisze
{
    public class EnovaAutomator
    {
        // Elastyczne usypianie reagujące na Stop i Pauzę
        private static void AktywnySleep(int milliseconds, CancellationToken token, ManualResetEventSlim pauseEvent)
        {
            int step = 100;
            int elapsed = 0;
            while (elapsed < milliseconds)
            {
                pauseEvent.Wait(token); // Zatrzymuje pętlę, dopóki kliknięta jest Pauza
                token.ThrowIfCancellationRequested(); // Rzuca wyjątek przerywający proces, jeśli kliknięto Stop

                Thread.Sleep(Math.Min(step, milliseconds - elapsed));
                elapsed += step;
            }
        }

        // Zaktualizowana sygnatura - przyjmuje token i zdarzenie pauzy
        public static void Uruchom(List<string> listaBaz, string login, string haslo, string nowyOperator, string hasloOperatora, string sciezkaXml, string sciezkaEnova, CancellationToken token, ManualResetEventSlim pauseEvent, Action<string> log)
        {
            log($"Uruchamianie Enova365 ze ścieżki: {sciezkaEnova}");

            try
            {
                var processInfo = new ProcessStartInfo(sciezkaEnova)
                {
                    UseShellExecute = true
                };

                var startedProcess = Process.Start(processInfo);
                log("Enova365 została uruchomiona niezależnie.");
                AktywnySleep(6000, token, pauseEvent);

                if (listaBaz == null || listaBaz.Count == 0)
                {
                    log("BŁĄD: Brak zaznaczonych baz do przetestowania!");
                    return;
                }

                // Działamy stabilnie tylko na jednej bazie, tak jak w starym kodzie
                string nazwaBazy = listaBaz[0];
                log($"---> TEST JEDNEJ BAZY: {nazwaBazy} <---");

                using (var automation = new UIA3Automation())
                {
                    if (startedProcess == null)
                    {
                        log("BŁĄD: Nie udało się uchwycić procesu Enovy.");
                        return;
                    }

                    var app = FlaUI.Core.Application.Attach(startedProcess);
                    var mainWindow = app.GetMainWindow(automation);
                    mainWindow.WaitUntilClickable(TimeSpan.FromSeconds(15));
                    log("Pobrano główne okno Enovy.");

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
                    {
                        poleWyszukiwania = wszystkiePolaEdit[0].AsTextBox();
                    }

                    if (poleWyszukiwania != null)
                    {
                        string szukanaFraza = $"\"{nazwaBazy}\"";
                        poleWyszukiwania.Focus();
                        AktywnySleep(500, token, pauseEvent);

                        poleWyszukiwania.Text = szukanaFraza;
                        log($"Filtruję listę dla: {szukanaFraza}");
                        AktywnySleep(1500, token, pauseEvent);

                        // Pobieramy absolutnie wszystko, co nazywa się jak nasza baza
                        var znalezioneElementy = mainWindow.FindAllDescendants(cf => cf.ByName(nazwaBazy));
                        AutomationElement elementBazy = null;

                        if (znalezioneElementy.Length > 0)
                        {
                            // TARCZA SNAJPERSKA: Szukamy tylko "gołego" tekstu! 
                            // Ignorujemy zbugowane, wielkie kontenery, które zachodzą na nagłówek "Firmy".
                            elementBazy = znalezioneElementy.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Text);

                            // Koło ratunkowe: jakby Enova ukryła typ Text, bierzemy po prostu pierwszy element
                            if (elementBazy == null)
                            {
                                elementBazy = znalezioneElementy[0];
                            }
                        }

                        if (elementBazy != null)
                        {
                            log($"Zlokalizowano bazę '{nazwaBazy}'. Wykonuję precyzyjny dwuklik w sam tekst...");

                            try
                            {
                                elementBazy.Click();
                                AktywnySleep(200, token, pauseEvent);
                                elementBazy.DoubleClick();
                            }
                            catch { }

                            log($"SUKCES: Wysłano fizyczny dwuklik w bazę: {nazwaBazy}");

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
                            catch { /* Ignorujemy błędy */ }

                            if (oknoAktualizacji != null)
                            {
                                log("Wykryto okno aktualizacji! Klikam 'Tak'...");
                                var btnTak = oknoAktualizacji.FindFirstDescendant(cf => cf.ByName("Tak"))?.AsButton();
                                if (btnTak != null) btnTak.Click();
                                else { oknoAktualizacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }

                                log("Czekam 15 sekund na całkowity reset Enovy...");
                                AktywnySleep(15000, token, pauseEvent);

                                string nazwaProcesu = System.IO.Path.GetFileNameWithoutExtension(sciezkaEnova);
                                var procesy = Process.GetProcessesByName(nazwaProcesu);

                                if (procesy.Length > 0)
                                {
                                    app = FlaUI.Core.Application.Attach(procesy[0]);
                                    log("Ponownie podpięto się pod zresetowany proces Enovy.");
                                }
                                else
                                {
                                    log("BŁĄD: Po aktualizacji dodatków Enova nie wstała ponownie!");
                                    return;
                                }
                            }
                        }
                        else
                        {
                            log($"BŁĄD: Nie znaleziono bazy '{nazwaBazy}'!");
                            return;
                        }
                    }
                    else
                    {
                        log("BŁĄD: Nie znaleziono pola wyszukiwania bazy!");
                        return;
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
                                if (wnd.Name.Contains("Logowanie do bazy")) { oknoLogowania = wnd; break; }
                                foreach (var modal in wnd.ModalWindows)
                                {
                                    if (modal.Name.Contains("Logowanie do bazy")) { oknoLogowania = modal; break; }
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
                        log("Wykryto okno logowania. Wpisuję dane...");
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

                        log("Zatwierdzono logowanie. Sprawdzam status (hasło, konwersja, błędy)...");

                        bool zlyLogin = false;
                        bool wymagaKonwersji = false;

                        // Pętla sprawdzająca z potężną optymalizacją szybkości
                        for (int i = 0; i < 12; i++)
                        {
                            pauseEvent.Wait(token);

                            Window errorWindow = null;
                            Window konwersjaWindow = null;

                            // 1. Bardzo szybkie sprawdzenie lokalnych błędów (ModalWindows)
                            try
                            {
                                konwersjaWindow = oknoLogowania.ModalWindows.FirstOrDefault(m => m.Name != null && m.Name.Contains("Konwersja bazy"));
                                errorWindow = oknoLogowania.ModalWindows.FirstOrDefault(m => m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd")));

                                if (errorWindow == null)
                                {
                                    foreach (var m in oknoLogowania.ModalWindows)
                                    {
                                        if (m.FindFirstDescendant(cf => cf.ByName("Raport błędu")) != null)
                                        {
                                            errorWindow = m;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch { }

                            // 2. Jeśli modale nic nie zwróciły - robimy JEDEN zrzut wszystkich okien (oszczędność czasu procesora!)
                            if (konwersjaWindow == null && errorWindow == null)
                            {
                                try
                                {
                                    var topWindows = app.GetAllTopLevelWindows(automation);

                                    konwersjaWindow = topWindows.FirstOrDefault(w => w.Name != null && w.Name.Contains("Konwersja bazy"));
                                    errorWindow = topWindows.FirstOrDefault(w => w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")));

                                    // TARCZA PRZYSPIESZAJĄCA: Czy okno logowania w ogóle jeszcze istnieje?
                                    bool logowanieIstnieje = topWindows.Any(w => w.Name != null && w.Name.Contains("Logowanie do bazy"));
                                    if (!logowanieIstnieje)
                                    {
                                        break; // Okno zniknęło, logowanie udane - błyskawiczna ucieczka z pętli!
                                    }
                                }
                                catch { }
                            }

                            if (konwersjaWindow != null)
                            {
                                wymagaKonwersji = true;
                                log("UWAGA: Baza pochodzi ze starszej wersji! Klikam 'Anuluj' na oknie konwersji...");

                                var btnAnulujKonwersje = konwersjaWindow.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                if (btnAnulujKonwersje != null) btnAnulujKonwersje.Click();
                                else { konwersjaWindow.Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); }

                                AktywnySleep(1000, token, pauseEvent);
                                break;
                            }

                            if (errorWindow != null)
                            {
                                zlyLogin = true;
                                log("BŁĄD: Wprowadzono nieprawidłowy login lub hasło (konto zablokowane)!");

                                var btnOkError = errorWindow.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                if (btnOkError != null) btnOkError.Click();
                                else { errorWindow.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }

                                AktywnySleep(1000, token, pauseEvent);
                                break;
                            }

                            if (wymagaKonwersji || zlyLogin) break;
                            AktywnySleep(500, token, pauseEvent);
                        }

                        // --- OBSŁUGA PO PRZERWANIU KONWERSJI ---
                        if (wymagaKonwersji)
                        {
                            log("Oczekuję na okno błędu po anulowaniu konwersji...");
                            AktywnySleep(1000, token, pauseEvent);

                            try
                            {
                                var errorWindow = oknoLogowania.ModalWindows.FirstOrDefault(m => m.Name != null && (m.Name.Contains("Stop") || m.Name.Contains("Błąd")));
                                if (errorWindow == null)
                                {
                                    var topWindows = app.GetAllTopLevelWindows(automation);
                                    errorWindow = topWindows.FirstOrDefault(w => w.Name != null && (w.Name.Contains("Stop") || w.Name.Contains("Błąd")));
                                }

                                if (errorWindow != null)
                                {
                                    var btnOkError = errorWindow.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                    if (btnOkError != null) btnOkError.Click();
                                    else { errorWindow.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                                    log("Kliknięto OK na komunikacie przerwania.");
                                }
                            }
                            catch { }

                            AktywnySleep(1000, token, pauseEvent);

                            log("Zamykam okno logowania (Anuluj)...");
                            try
                            {
                                var btnAnuluj = oknoLogowania.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                if (btnAnuluj != null) btnAnuluj.Click();
                                else { oknoLogowania.Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); }
                            }
                            catch { }

                            log($"PRZERYWAM PROCES: Baza '{nazwaBazy}' pominięta ze względu na starą wersję. Wyłączam proces Enovy...");
                            try { app.Close(); } catch { }
                            return;
                        }

                        // --- OBSŁUGA PO BŁĘDNYM HAŚLE ---
                        if (zlyLogin)
                        {
                            log("Zamykam okno logowania (Anuluj)...");
                            try
                            {
                                var btnAnuluj = oknoLogowania.FindFirstDescendant(cf => cf.ByName("Anuluj"))?.AsButton();
                                if (btnAnuluj != null) btnAnuluj.Click();
                                else { oknoLogowania.Focus(); Keyboard.Press(VirtualKeyShort.ESCAPE); }
                            }
                            catch { }

                            log($"PRZERYWAM PROCES: Baza '{nazwaBazy}' odrzuciła dane logowania. Wyłączam proces Enovy...");
                            try { app.Close(); } catch { }
                            return;
                        }

                        log("Logowanie poprawne. Oczekuję na załadowanie bazy...");

                        // Ucinamy sztywne 5 sekund. Dajemy tylko 1.5s na przepięcie procesów w pamięci.
                        AktywnySleep(1500, token, pauseEvent);
                        try { mainWindow = app.GetMainWindow(automation); }
                        catch { AktywnySleep(1500, token, pauseEvent); mainWindow = app.GetMainWindow(automation); }
                    }
                    else
                    {
                        // TEGO BRAKOWAŁO! Skrypt milczał, gdy okno logowania się nie pojawiało.
                        log($"BŁĄD KRYTYCZNY: Okno logowania nie pojawiło się dla bazy '{nazwaBazy}'!");
                        log("Prawdopodobnie baza się nie otworzyła. Przerywam proces, żeby nie zawiesić skryptu.");
                        try { app.Close(); } catch { }
                        return; // Uciekamy z metody
                    }

                    // ==========================================
                    // KROK 3: LICENCJE
                    // ==========================================
                    log("Sprawdzam okno licencji...");

                    FlaUI.Core.AutomationElements.Button btnOdznacz = null;
                    Window oknoLicencji = mainWindow;

                    // INTELIGENTNA PĘTLA: Szukamy przycisku przez max 10 sekund (20 x 500ms). 
                    // Jak znajdzie wcześniej - uderza od razu!
                    for (int j = 0; j < 20; j++)
                    {
                        pauseEvent.Wait(token);
                        try
                        {
                            if (mainWindow != null)
                            {
                                btnOdznacz = mainWindow.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje"))?.AsButton();

                                if (btnOdznacz == null)
                                {
                                    foreach (var modal in mainWindow.ModalWindows)
                                    {
                                        btnOdznacz = modal.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje"))?.AsButton();
                                        if (btnOdznacz != null) { oknoLicencji = modal; break; }
                                    }
                                }
                            }

                            if (btnOdznacz == null)
                            {
                                var wszystkieOkna = app.GetAllTopLevelWindows(automation);
                                foreach (var wnd in wszystkieOkna)
                                {
                                    btnOdznacz = wnd.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje"))?.AsButton();
                                    if (btnOdznacz != null) { oknoLicencji = wnd; break; }
                                }
                            }
                        }
                        catch { }

                        if (btnOdznacz != null) break; // Znalazł? Uciekamy z pętli natychmiast!
                        AktywnySleep(500, token, pauseEvent); // Nie znalazł? Czeka tylko pół sekundy i szuka znów.
                    }

                    if (btnOdznacz != null && oknoLicencji != null)
                    {
                        log("Znaleziono licencje. Klikam 'Odznacz niedostępne licencje'...");
                        btnOdznacz.Click();
                        AktywnySleep(1500, token, pauseEvent);

                        var btnZapisz = oknoLicencji.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                        if (btnZapisz != null)
                        {
                            btnZapisz.Click();
                            log("Kliknięto 'Zapisz i zamknij'.");
                            AktywnySleep(3000, token, pauseEvent);
                        }
                    }
                    else
                    {
                        log("Nie znaleziono okna licencji, przechodzę dalej...");
                    }

                    try { mainWindow = app.GetMainWindow(automation); } catch { }

                    // ==========================================
                    // KROK 4: IMPORT XML
                    // ==========================================
                    log("Otwieram menu Plik -> Importuj zapisy -> Z pliku XML...");

                    mainWindow.Focus();
                    AktywnySleep(500, token, pauseEvent);

                    using (Keyboard.Pressing(VirtualKeyShort.ALT))
                    {
                        Keyboard.Press(VirtualKeyShort.KEY_P);
                    }
                    AktywnySleep(600, token, pauseEvent);

                    Keyboard.Press(VirtualKeyShort.KEY_I);
                    AktywnySleep(600, token, pauseEvent);

                    Keyboard.Press(VirtualKeyShort.KEY_Z);
                    AktywnySleep(1500, token, pauseEvent);

                    log("Oczekuję na systemowe okno wyboru pliku...");
                    Window oknoOtwierania = null;
                    for (int i = 0; i < 20; i++)
                    {
                        pauseEvent.Wait(token);
                        oknoOtwierania = mainWindow.ModalWindows.FirstOrDefault(m => m.Name.Contains("Otwieranie") || m.Name.Contains("Open"));

                        if (oknoOtwierania == null)
                        {
                            var topWindows = app.GetAllTopLevelWindows(automation);
                            oknoOtwierania = topWindows.FirstOrDefault(m => m.Name.Contains("Otwieranie") || m.Name.Contains("Open"));
                        }

                        if (oknoOtwierania != null) break;
                        AktywnySleep(500, token, pauseEvent);
                    }

                    if (oknoOtwierania != null)
                    {
                        log($"Wpisuję ścieżkę do pliku XML: {sciezkaXml}");
                        oknoOtwierania.Focus();
                        AktywnySleep(1000, token, pauseEvent);

                        Keyboard.Type(sciezkaXml);
                        AktywnySleep(1000, token, pauseEvent);
                        Keyboard.Press(VirtualKeyShort.ENTER);

                        log("Oczekuję na komunikat o pomyślnym imporcie...");
                        Window oknoInformacji = null;
                        for (int i = 0; i < 20; i++)
                        {
                            pauseEvent.Wait(token);
                            oknoInformacji = mainWindow.ModalWindows.FirstOrDefault(m => m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja"));

                            if (oknoInformacji == null)
                            {
                                var topWindows = app.GetAllTopLevelWindows(automation);
                                oknoInformacji = topWindows.FirstOrDefault(m => m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja"));
                            }

                            if (oknoInformacji != null) break;
                            AktywnySleep(500, token, pauseEvent);
                        }

                        if (oknoInformacji != null)
                        {
                            log("Import zakończony! Klikam 'OK'...");
                            var btnOkInfo = oknoInformacji.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                            if (btnOkInfo != null) btnOkInfo.Click();
                            else { oknoInformacji.Focus(); Keyboard.Press(VirtualKeyShort.ENTER); }
                        }
                        else
                        {
                            log("OSTRZEŻENIE: Nie znalazłem okienka z potwierdzeniem importu. Idę dalej...");
                        }
                    }

                    // ==========================================
                    // KROK 5: WYSZUKANIE OPERATORA I ZMIANA HASŁA
                    // ==========================================
                    AktywnySleep(2000, token, pauseEvent);
                    log("Otwieram listę operatorów (skrót: Ctrl+F9, Ctrl+O)...");

                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
                    {
                        Keyboard.Press(VirtualKeyShort.F9);
                    }
                    AktywnySleep(1500, token, pauseEvent);

                    using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
                    {
                        Keyboard.Press(VirtualKeyShort.KEY_O);
                    }

                    log("Czekam na załadowanie listy operatorów...");
                    AktywnySleep(3000, token, pauseEvent);

                    log($"Skanuję całą tabelę w poszukiwaniu: {nowyOperator}...");
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
                                    if (nazwaElementu.Trim().Equals(nowyOperator.Trim(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        wpisOperatora = el;
                                        break;
                                    }

                                    if (el.ControlType == FlaUI.Core.Definitions.ControlType.DataItem && nazwaElementu.Contains(nowyOperator))
                                    {
                                        wpisOperatora = el;
                                        break;
                                    }
                                }

                                if (el.Patterns.Value.IsSupported)
                                {
                                    string wartosc = el.Patterns.Value.Pattern.Value.Value;
                                    if (!string.IsNullOrWhiteSpace(wartosc) && wartosc.Trim().Equals(nowyOperator.Trim(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        wpisOperatora = el;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (wpisOperatora != null) break;
                        AktywnySleep(1000, token, pauseEvent);
                    }

                    if (wpisOperatora != null)
                    {
                        log("Znalazłem operatora! Zaznaczam...");

                        wpisOperatora.Click();
                        AktywnySleep(1000, token, pauseEvent);

                        var btnUstawHaslo = mainWindow.FindFirstDescendant(cf => cf.ByName("Ustaw hasło..."))?.AsButton()
                                            ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Ustaw hasło"))?.AsButton();

                        if (btnUstawHaslo != null)
                        {
                            log("Klikam 'Ustaw hasło...'");
                            btnUstawHaslo.Click();

                            log("Oczekuję na okno ustawiania hasła...");
                            Window oknoUstawiania = null;

                            for (int k = 0; k < 20; k++)
                            {
                                pauseEvent.Wait(token);
                                var wszystkieOkna = app.GetAllTopLevelWindows(automation);
                                foreach (var w in wszystkieOkna)
                                {
                                    if (!string.IsNullOrEmpty(w.Name) && (w.Name.IndexOf("hasł", StringComparison.OrdinalIgnoreCase) >= 0 || w.Name.IndexOf("Ustawien", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        oknoUstawiania = w;
                                        break;
                                    }

                                    var btnBrak = w.FindFirstDescendant(cf => cf.ByName("Brak"))?.AsButton();
                                    if (btnBrak != null)
                                    {
                                        oknoUstawiania = w;
                                        break;
                                    }
                                }

                                if (oknoUstawiania == null)
                                {
                                    foreach (var m in mainWindow.ModalWindows)
                                    {
                                        if (!string.IsNullOrEmpty(m.Name) && (m.Name.IndexOf("hasł", StringComparison.OrdinalIgnoreCase) >= 0))
                                        {
                                            oknoUstawiania = m;
                                            break;
                                        }
                                    }
                                }

                                if (oknoUstawiania != null) break;
                                AktywnySleep(500, token, pauseEvent);
                            }

                            if (oknoUstawiania != null)
                            {
                                log("Wykryto okno. Wpisuję nowe hasło x2...");
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
                                if (btnOkHaslo != null)
                                {
                                    btnOkHaslo.Click();
                                }
                                else
                                {
                                    Keyboard.Press(VirtualKeyShort.ENTER);
                                }

                                log("Hasło zapisane. Szukam przycisku 'Zapisz i zamknij'...");
                                AktywnySleep(2500, token, pauseEvent);

                                var btnZapiszKoncowe = mainWindow.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                                if (btnZapiszKoncowe != null)
                                {
                                    btnZapiszKoncowe.Click();
                                    log("✅ OPERACJA DLA BAZY ZAKOŃCZONA PEŁNYM SUKCESEM!");
                                    AktywnySleep(2000, token, pauseEvent);
                                }
                                else
                                {
                                    log("OSTRZEŻENIE: Nie mogłem zlokalizować przycisku 'Zapisz i zamknij' na sam koniec.");
                                }
                            }
                            else
                            {
                                log("BŁĄD: Okno 'Ustawienie hasła dostępu' nie zostało rozpoznane przez system!");
                            }
                        }
                        else
                        {
                            log("BŁĄD: Nie znaleziono przycisku 'Ustaw hasło...' na pasku.");
                        }
                    }
                    else
                    {
                        log($"BŁĄD: Operator '{nowyOperator}' nie pojawił się na liście po imporcie (albo jest niewidoczny dla skanera)!");
                    }
                }

                log("KONIEC TESTU BAZY!");
            }
            catch (OperationCanceledException)
            {
                log("\n🛑 AUTOMATYZACJA ZOSTAŁA PRZERWANA NA ŻĄDANIE UŻYTKOWNIKA.");
            }
            catch (Exception ex)
            {
                log($"BŁĄD KRYTYCZNY AUTOMATYZACJI: {ex.Message}");
            }
        }
    }
}