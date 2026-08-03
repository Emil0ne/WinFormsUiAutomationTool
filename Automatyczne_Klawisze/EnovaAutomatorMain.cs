using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Automatyczne_Klawisze
{
    public class EnovaAutomatorMain
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
                Process.Start(processInfo);

                log("Enova365 została uruchomiona niezależnie.");
                Thread.Sleep(3000);

                /*foreach (var nazwaBazy in listaBaz)
                {
                    log($"---> Przetwarzanie bazy: {nazwaBazy} <---");

                    try
                    {
                        // KROK 1: Wyszukanie i kliknięcie bazy na lewym drzewku
                        log("Szukam pola wyboru bazy...");
                        var poleWyszukiwania = mainWindow.FindFirstDescendant(cf => cf.ByName("Szukaj folderu...")).AsTextBox();

                        if (poleWyszukiwania != null)
                        {
                            // Wpisujemy nazwę bazy w cudzysłowie dla lepszego filtrowania przez Enovę
                            string szukanaFraza = $"\"{nazwaBazy}\"";
                            poleWyszukiwania.Text = szukanaFraza;
                            Thread.Sleep(1000); // Dajemy chwilę na przefiltrowanie listy przez Enovę

                            log(($"Filtruję listę dla: {szukanaFraza}"));

                            // Szukamy na drzewku po lewej stronie konkretnego elementu odpowiadającego naszej bazie
                            // Enova wyświetla je w sekcji "Firmy", więc szukamy elementu o dokładnej nazwie bazy
                            var elementBazy = mainWindow.FindFirstDescendant(cf => cf.ByName(nazwaBazy));

                            if (elementBazy != null)
                            {
                                // Klikamy dwukrotnie w odnalezioną bazę na liście, aby ją otworzyć
                                elementBazy.Click();
                                Thread.Sleep(200);
                                elementBazy.Click(); // Drugi klik (Double Click) otwiera bazę
                                log($"Kliknięto dwukrotnie w bazę: {nazwaBazy}");
                            }
                            else
                            {
                                log($"BŁĄD: Nie znaleziono bazy '{nazwaBazy}' na przefiltrowanej liście!");
                                continue;
                            }
                        }
                        else
                        {
                            log("BŁĄD: Nie znaleziono pola wyszukiwania bazy w oknie głównym!");
                            continue;
                        }

                        // KROK 2: Oczekiwanie na okno logowania do bazy
                        Thread.Sleep(2000);
                        log("Szukam okna logowania do bazy...");

                        // Szukamy okna logowania (może być oknem modalnym aplikacji)
                        Window oknoLogowania = null;
                        for (int i = 0; i < 10; i++) // Czekamy max 5 sekund
                        {
                            var modale = mainWindow.ModalWindows;
                            if (modale.Length > 0 && modale[0].Name.Contains("Logowanie do bazy"))
                            {
                                oknoLogowania = modale[0];
                                break;
                            }
                            Thread.Sleep(500);
                        }

                        if (oknoLogowania != null)
                        {
                            log("Znaleziono okno logowania. Wprowadzam dane...");

                            // Szukamy pól tekstowych w oknie logowania
                            var txtUser = oknoLogowania.FindFirstDescendant(cf => cf.ByName("Nazwa użytkownika:")).AsTextBox();
                            var txtPass = oknoLogowania.FindFirstDescendant(cf => cf.ByName("Hasło dostępu:")).AsTextBox();

                            if (txtUser != null && txtPass != null)
                            {
                                txtUser.Text = login;
                                txtPass.Text = haslo;
                                log("Wpisano login i hasło.");

                                // Klikamy przycisk OK
                                var btnOk = oknoLogowania.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
                                if (btnOk != null)
                                {
                                    btnOk.Click();
                                    log("Kliknięto przycisk OK w oknie logowania.");
                                }
                                else
                                {
                                    Keyboard.Press(VirtualKeyShort.ENTER);
                                    log("Zatwierdzono Enterem.");
                                }
                            }
                            else
                            {
                                log("BŁĄD: Nie znaleziono pól tekstowych loginu lub hasła w oknie logowania.");
                            }
                        }
                        else
                        {
                            log("OSTRZEŻENIE: Nie wykryto okna logowania do bazy.");
                        }

                        // KROK 3: Sprawdzenie ewentualnego okna licencji (które mogło wyskoczyć po logowaniu)
                        Thread.Sleep(2000);
                        var oknaModalnePoLogowaniu = mainWindow.ModalWindows;
                        foreach (var okno in oknaModalnePoLogowaniu)
                        {
                            if (okno.Name.Contains("Pobrane licencje"))
                            {
                                log("Wykryto okno zarządzania licencjami po zalogowaniu.");

                                var btnOdznacz = okno.FindFirstDescendant(cf => cf.ByName("Odznacz niedostępne licencje"))?.AsButton();
                                if (btnOdznacz != null)
                                {
                                    btnOdznacz.Click();
                                    log("Kliknięto: Odznacz niedostępne licencje.");
                                    Thread.Sleep(1000);
                                }

                                var btnZapiszZamknij = okno.FindFirstDescendant(cf => cf.ByName("Zapisz i zamknij"))?.AsButton();
                                if (btnZapiszZamknij != null)
                                {
                                    btnZapiszZamknij.Click();
                                    log("Kliknięto: Zapisz i zamknij.");
                                    Thread.Sleep(2000);
                                }
                                break;
                            }
                        }

                        // Dajemy czas na wejście do głównego widoku bazy
                        Thread.Sleep(4000);
                        log($"Baza {nazwaBazy} – zalogowano pomyślnie.");

                    }
                    catch (Exception exBaza)
                    {
                        log($"BŁĄD przy bazie {nazwaBazy}: {exBaza.Message}");
                    }
                }
                */
                log("KONIEC PRACY! Pętla baz zakończona.");
                 
            }
            catch (Exception ex)
            {
                log($"BŁĄD KRYTYCZNY AUTOMATYZACJI: {ex.Message}");
            }
        }
    }
}