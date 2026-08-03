using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Automatyczne_Klawisze
{
    public class EnovaAutomator
    {
        // Metoda przyjmuje teraz również ścieżkę do wybranego przez użytkownika pliku .exe Enovy
        public static void Uruchom(List<string> listaBaz, string login, string haslo, string nowyOperator, string hasloOperatora, string sciezkaXml, string sciezkaEnova, Action<string> log)
        {
            log($"Uruchamianie Enova365 ze ścieżki: {sciezkaEnova}");

            try
            {
                var app = FlaUI.Core.Application.Launch(new ProcessStartInfo(sciezkaEnova));

                using (var automation = new UIA3Automation())
                {
                    var mainWindow = app.GetMainWindow(automation);
                    mainWindow.WaitUntilClickable(TimeSpan.FromSeconds(15));
                    log("Aplikacja Enova365 uruchomiona poprawnie. Rozpoczynam pętlę.");

                    foreach (var nazwaBazy in listaBaz)
                    {
                        log($"---> Przetwarzanie bazy: {nazwaBazy} <---");

                        // KROK 1: Wyszukanie bazy na liście
                        var poleWyszukiwania = mainWindow.FindFirstDescendant(cf => cf.ByName("Szukaj folderu...")).AsTextBox();
                        poleWyszukiwania.Text = nazwaBazy;
                        Thread.Sleep(500); // Małe opóźnienie dla stabilności
                        Keyboard.Press(VirtualKeyShort.ENTER);

                        log($"Otwieram okno logowania dla {nazwaBazy}...");

                        // Tutaj w kolejnym kroku dopiszemy wpisywanie loginu (login), hasła (haslo)
                        // oraz wyklikanie importu pliku XML (sciezkaXml) i dodanie operatora (nowyOperator, hasloOperatora)

                        Thread.Sleep(3000);
                        log($"Baza {nazwaBazy} przetworzona wstępnie. Przechodzę dalej.");

                        // ZAMKNIĘCIE BAZY (np. skrótem Ctrl+F4 lub obsługa okna głównego)
                        // Keyboard.Pressing(VirtualKeyShort.CONTROL);
                        // Keyboard.Press(VirtualKeyShort.F4);
                        // Keyboard.Release(VirtualKeyShort.CONTROL);
                        // Thread.Sleep(1000);
                    }

                    log("KONIEC PRACY! Przetworzono wszystkie zaznaczone bazy.");
                }
            }
            catch (Exception ex)
            {
                log($"BŁĄD KRYTYCZNY AUTOMATYZACJI: {ex.Message}");
            }
        }
    }
}