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
        public static void Uruchom(List<string> listaBaz, string login, string haslo, string nowyOperator, string hasloOperatora, string sciezkaXml, string sciezkaEnova, Action<string> log)
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
                Thread.Sleep(6000);

                if (listaBaz == null || listaBaz.Count == 0)
                {
                    log("BŁĄD: Brak zaznaczonych baz do przetestowania!");
                    return;
                }

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
                        Thread.Sleep(500);

                        poleWyszukiwania.Text = szukanaFraza;
                        log($"Filtruję listę dla: {szukanaFraza}");
                        Thread.Sleep(1500);

                        var elementBazy = mainWindow.FindFirstDescendant(cf => cf.ByName(nazwaBazy));

                        if (elementBazy != null)
                        {
                            elementBazy.Click();
                            Thread.Sleep(200);
                            elementBazy.DoubleClick();
                            log($"SUKCES: Kliknięto dwukrotnie w bazę: {nazwaBazy}");

                            // --- OBSŁUGA OKNA AKTUALIZACJI DODATKÓW ---
                            Thread.Sleep(3000);
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
                                Thread.Sleep(15000);

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
                        Thread.Sleep(500);
                    }

                    if (oknoLogowania != null)
                    {
                        log("Wykryto okno logowania. Wpisuję dane...");
                        oknoLogowania.Focus();
                        Thread.Sleep(500);

                        Keyboard.Type(login);
                        Thread.Sleep(300);
                        Keyboard.Press(VirtualKeyShort.TAB);
                        Thread.Sleep(300);

                        if (!string.IsNullOrEmpty(haslo))
                        {
                            Keyboard.Type(haslo);
                            Thread.Sleep(300);
                        }

                        var btnOk = oknoLogowania.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                        if (btnOk != null) btnOk.Click();
                        else Keyboard.Press(VirtualKeyShort.ENTER);

                        log("Zatwierdzono logowanie.");
                        Thread.Sleep(8000);
                        try { mainWindow = app.GetMainWindow(automation); }
                        catch { Thread.Sleep(3000); mainWindow = app.GetMainWindow(automation); }
                    }

                    // ==========================================
                    // KROK 3: LICENCJE
                    // ==========================================
                    if (mainWindow != null)
                    {
                        log("Sprawdzam okno licencji...");
                        Thread.Sleep(5000);

                        var btnOdznacz = mainWindow.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje"))?.AsButton();
                        if (btnOdznacz == null)
                        {
                            foreach (var modal in mainWindow.ModalWindows)
                            {
                                btnOdznacz = modal.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje"))?.AsButton();
                                if (btnOdznacz != null) break;
                            }
                        }

                        if (btnOdznacz != null)
                        {
                            log("Klikam 'Odznacz niedostępne licencje'...");
                            btnOdznacz.Click();
                            Thread.Sleep(1500);

                            var btnZapisz = mainWindow.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                            if (btnZapisz == null)
                            {
                                foreach (var modal in mainWindow.ModalWindows)
                                {
                                    btnZapisz = modal.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                                    if (btnZapisz != null) break;
                                }
                            }

                            if (btnZapisz != null)
                            {
                                btnZapisz.Click();
                                log("Kliknięto 'Zapisz i zamknij'.");
                                Thread.Sleep(3000);
                            }
                        }

                        // ==========================================
                        // KROK 4: IMPORT XML
                        // ==========================================
                        log("Otwieram menu Plik -> Importuj zapisy -> Z pliku XML...");

                        // ZAMIAST KLIKAĆ PO ZBUGOWANYM MENU, UŻYWAMY TWARDEJ KLAWIATURY
                        mainWindow.Focus();
                        Thread.Sleep(500);

                        // Krok 1: Wciskamy Alt + P (Otwiera menu Plik)
                        using (Keyboard.Pressing(VirtualKeyShort.ALT))
                        {
                            Keyboard.Press(VirtualKeyShort.KEY_P);
                        }
                        Thread.Sleep(600); // Dajemy Enovie czas na wyrysowanie menu

                        // Krok 2: Wciskamy I (Wybiera 'Importuj zapisy')
                        Keyboard.Press(VirtualKeyShort.KEY_I);
                        Thread.Sleep(600);

                        // Krok 3: Wciskamy Z (Wybiera 'Z pliku XML...')
                        Keyboard.Press(VirtualKeyShort.KEY_Z);
                        Thread.Sleep(1500); // Tutaj dłuższą chwilę na załadowanie systemowego okna Windows

                        log("Oczekuję na systemowe okno wyboru pliku...");
                        Window oknoOtwierania = null;
                        for (int i = 0; i < 20; i++)
                        {
                            // Szukamy okna modalnego i ogólnego
                            oknoOtwierania = mainWindow.ModalWindows.FirstOrDefault(m => m.Name.Contains("Otwieranie") || m.Name.Contains("Open"));

                            if (oknoOtwierania == null)
                            {
                                var topWindows = app.GetAllTopLevelWindows(automation);
                                oknoOtwierania = topWindows.FirstOrDefault(m => m.Name.Contains("Otwieranie") || m.Name.Contains("Open"));
                            }

                            if (oknoOtwierania != null) break;
                            Thread.Sleep(500);
                        }

                        if (oknoOtwierania != null)
                        {
                            log($"Wpisuję ścieżkę do pliku XML: {sciezkaXml}");
                            oknoOtwierania.Focus();
                            Thread.Sleep(1000); // Ważne: dajemy systemowi chwilę na ustawienie kursora w polu

                            Keyboard.Type(sciezkaXml);
                            Thread.Sleep(1000);
                            Keyboard.Press(VirtualKeyShort.ENTER);

                            log("Oczekuję na komunikat o pomyślnym imporcie...");
                            Window oknoInformacji = null;
                            for (int i = 0; i < 20; i++)
                            {
                                oknoInformacji = mainWindow.ModalWindows.FirstOrDefault(m => m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja"));

                                if (oknoInformacji == null)
                                {
                                    var topWindows = app.GetAllTopLevelWindows(automation);
                                    oknoInformacji = topWindows.FirstOrDefault(m => m.Name.Contains("Informacja - enova365") || m.Name.Contains("Informacja"));
                                }

                                if (oknoInformacji != null) break;
                                Thread.Sleep(500);
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
                        Thread.Sleep(2000); // Dajemy czas na zamknięcie się popupów po imporcie
                        log("Otwieram listę operatorów (skrót: Ctrl+F9, Ctrl+O)...");

                        using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
                        {
                            Keyboard.Press(VirtualKeyShort.F9);
                        }
                        Thread.Sleep(1500);

                        using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
                        {
                            Keyboard.Press(VirtualKeyShort.KEY_O);
                        }

                        log("Czekam na załadowanie listy operatorów...");
                        Thread.Sleep(3000);

                        log($"Skanuję całą tabelę w poszukiwaniu: {nowyOperator}...");
                        AutomationElement wpisOperatora = null;

                        // Skaner totalny - szukamy na wszystkie możliwe sposoby
                        for (int i = 0; i < 5; i++)
                        {
                            var wszystkieElementy = mainWindow.FindAllDescendants();
                            foreach (var el in wszystkieElementy)
                            {
                                try
                                {
                                    string nazwaElementu = el.Name;

                                    if (!string.IsNullOrWhiteSpace(nazwaElementu))
                                    {
                                        // 1. Dokładne dopasowanie nazwy
                                        if (nazwaElementu.Trim().Equals(nowyOperator.Trim(), StringComparison.OrdinalIgnoreCase))
                                        {
                                            wpisOperatora = el;
                                            break;
                                        }

                                        // 2. Szukanie w całych wierszach (Gridach) - Enova często łączy tekst wiersza
                                        if (el.ControlType == FlaUI.Core.Definitions.ControlType.DataItem && nazwaElementu.Contains(nowyOperator))
                                        {
                                            wpisOperatora = el;
                                            break;
                                        }
                                    }

                                    // 3. Sprawdzanie głęboko ukrytych wartości (np. w komórkach tabel)
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
                                catch { /* Ignorujemy błędy odczytu pojedynczych, zablokowanych elementów */ }
                            }

                            if (wpisOperatora != null) break;
                            Thread.Sleep(1000); // Dajemy liście czas, jeśli jeszcze doczytuje SQL
                        }

                        if (wpisOperatora != null)
                        {
                            log("Znalazłem operatora! Zaznaczam...");

                            // Przesuwamy myszkę i klikamy, Focus bywa zawodny w siatkach danych
                            wpisOperatora.Click();
                            Thread.Sleep(1000);

                            // Szukamy przycisku "Ustaw hasło..." - z kropkami lub bez
                            var btnUstawHaslo = mainWindow.FindFirstDescendant(cf => cf.ByName("Ustaw hasło..."))?.AsButton()
                                                ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Ustaw hasło"))?.AsButton();

                            if (btnUstawHaslo != null)
                            {
                                log("Klikam 'Ustaw hasło...'");
                                btnUstawHaslo.Click();

                                log("Oczekuję na okno ustawiania hasła...");
                                Window oknoUstawiania = null;

                                // Pętla szukająca okienka - uodporniona na dziwne nazwy Enovy
                                for (int k = 0; k < 20; k++)
                                {
                                    var wszystkieOkna = app.GetAllTopLevelWindows(automation);
                                    foreach (var w in wszystkieOkna)
                                    {
                                        // 1. Szukamy po luźnym fragmencie nazwy (ignorujemy wielkość liter i spacje)
                                        if (!string.IsNullOrEmpty(w.Name) && (w.Name.IndexOf("hasł", StringComparison.OrdinalIgnoreCase) >= 0 || w.Name.IndexOf("Ustawien", StringComparison.OrdinalIgnoreCase) >= 0))
                                        {
                                            oknoUstawiania = w;
                                            break;
                                        }

                                        // 2. TARCZA: Szukamy po charakterystycznym czerwonym przycisku "Brak" wewnątrz okna!
                                        var btnBrak = w.FindFirstDescendant(cf => cf.ByName("Brak"))?.AsButton();
                                        if (btnBrak != null)
                                        {
                                            oknoUstawiania = w;
                                            break;
                                        }
                                    }

                                    // Sprawdzamy też okna modalne głównego okna, dla pewności
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
                                    Thread.Sleep(500);
                                }

                                if (oknoUstawiania != null)
                                {
                                    log("Wykryto okno. Wpisuję nowe hasło x2...");
                                    oknoUstawiania.Focus();
                                    Thread.Sleep(1000); // Dajemy dłuższą chwilę na pełne aktywowanie okna

                                    // Znajdujemy pierwsze pole tekstowe w tym oknie i w nie klikamy, żeby kursor na pewno tam stał
                                    var pierwszePole = oknoUstawiania.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
                                    if (pierwszePole != null)
                                    {
                                        pierwszePole.Click();
                                        Thread.Sleep(300);
                                    }

                                    // Wpisujemy pierwsze hasło
                                    Keyboard.Type(hasloOperatora);
                                    Thread.Sleep(400);

                                    // Idziemy TAB-em do drugiego pola
                                    Keyboard.Press(VirtualKeyShort.TAB);
                                    Thread.Sleep(400);

                                    // Wpisujemy drugie hasło (potwierdzenie)
                                    Keyboard.Type(hasloOperatora);
                                    Thread.Sleep(400);

                                    // Szukamy i klikamy przycisk OK
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
                                    Thread.Sleep(2500);

                                    var btnZapiszKoncowe = mainWindow.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                                    if (btnZapiszKoncowe != null)
                                    {
                                        btnZapiszKoncowe.Click();
                                        log("✅ OPERACJA DLA BAZY ZAKOŃCZONA PEŁNYM SUKCESEM!");
                                        Thread.Sleep(2000);
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
                }

                log("KONIEC TESTU BAZY!");
            }
            catch (Exception ex)
            {
                log($"BŁĄD KRYTYCZNY AUTOMATYZACJI: {ex.Message}");
            }
        }
    }
}