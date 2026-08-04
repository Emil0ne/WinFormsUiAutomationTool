using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Automatyczne_Klawisze
{
    public partial class Form1 : Form
    {
        // ==========================================
        // ZMIENNE DO STEROWANIA PROCESEM
        // ==========================================
        private CancellationTokenSource _cts;
        private ManualResetEventSlim _pauseEvent;
        private bool _isPaused = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void btnWczytajBazy_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                openFileDialog.InitialDirectory = Path.Combine(appData, "Soneta");
                openFileDialog.Filter = "Pliki XML (*.xml)|*.xml|Wszystkie pliki (*.*)|*.*";
                openFileDialog.Title = "Wybierz plik z listą baz danych Enova";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        clbBazy.Items.Clear(); // Czyścimy starą listę
                        string wybranaSciezka = openFileDialog.FileName;

                        List<string> bazy = EnovaConfigReader.PobierzBazyZXml(wybranaSciezka);

                        if (bazy.Count > 0)
                        {
                            foreach (string baza in bazy)
                            {
                                clbBazy.Items.Add(baza);
                            }
                            rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] Pomyślnie wczytano {bazy.Count} baz z pliku XML.\n");
                        }
                        else
                        {
                            rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] OSTRZEŻENIE: Nie znaleziono baz w tym pliku!\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD wczytywania baz: {ex.Message}\n");
                    }
                }
            }
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void btnWybierzXml_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Pliki XML (*.xml)|*.xml|Wszystkie pliki (*.*)|*.*";
                openFileDialog.Title = "Wybierz plik XML do zaimportowania dla operatora";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtSciezkaXml.Text = openFileDialog.FileName;

                    rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] Wybrano plik do importu: {openFileDialog.FileName}\n");
                }
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSciezkaEnova.Text) || !File.Exists(txtSciezkaEnova.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Nie wybrano prawidłowego pliku uruchomieniowego Enova (.exe)!\n");
                return;
            }

            if (clbBazy.CheckedItems.Count == 0)
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Musisz zaznaczyć przynajmniej jedną bazę na liście!\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtEnovaUser.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Wprowadź login do Enovy!\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtNowyOperator.Text) || string.IsNullOrWhiteSpace(txtNoweHaslo.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Podaj kod i hasło dla nowego operatora!\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtSciezkaXml.Text) || !File.Exists(txtSciezkaXml.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Nie wybrano prawidłowego pliku XML do importu!\n");
                return;
            }

            List<string> bazyDoPrzetworzenia = new List<string>();
            foreach (var item in clbBazy.CheckedItems)
            {
                bazyDoPrzetworzenia.Add(item.ToString());
            }

            string login = txtEnovaUser.Text;
            string haslo = txtEnovaPass.Text;
            string nowyOp = txtNowyOperator.Text;
            string hasloOp = txtNoweHaslo.Text;
            string sciezkaXml = txtSciezkaXml.Text;
            string sciezkaEnova = txtSciezkaEnova.Text;

            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🚀 START AUTOMATYZACJI! Bazy do przetworzenia: {bazyDoPrzetworzenia.Count}\n");

            // ==========================================
            // INICJALIZACJA KONTROLERÓW WĄTKU (STOP I PAUZA)
            // ==========================================
            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true); // true = stan odblokowany (brak pauzy)
            _isPaused = false;

            // Jeśli kontrolka nazywa się inaczej, zmień tutaj "btnPauza" na swoją nazwę, np. "button2"
            btnPauza.Text = "Pauza";

            Task.Run(() =>
            {
                // Przekazujemy _cts.Token oraz _pauseEvent do naszej głównej metody
                EnovaAutomator.Uruchom(bazyDoPrzetworzenia, login, haslo, nowyOp, hasloOp, sciezkaXml, sciezkaEnova, _cts.Token, _pauseEvent, (wiadomosc) =>
                {
                    Invoke(new Action(() => rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] {wiadomosc}\n")));

                    Invoke(new Action(() =>
                    {
                        rtbLogi.SelectionStart = rtbLogi.Text.Length;
                        rtbLogi.ScrollToCaret();
                    }));
                });
            });
        }

        private void clbBazy_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnZaznaczWszystko_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbBazy.Items.Count; i++)
            {
                clbBazy.SetItemChecked(i, true);
            }
        }

        private void btnOdznaczWszystko_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbBazy.Items.Count; i++)
            {
                clbBazy.SetItemChecked(i, false);
            }
        }

        private void btnWybierzEnova_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Pliki wykonawcze (*.exe)|*.exe|Wszystkie pliki (*.*)|*.*";
                openFileDialog.Title = "Wybierz plik uruchomieniowy Enova (SonetaExplorer.exe)";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtSciezkaEnova.Text = openFileDialog.FileName;
                    rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] Wybrano plik Enova: {openFileDialog.FileName}\n");
                }
            }
        }

        private void rtbLogi_TextChanged(object sender, EventArgs e)
        {

        }

        // ==========================================
        // OBSŁUGA PRZYCISKU PAUZY
        // ==========================================
        private void btnPauza_Click(object sender, EventArgs e)
        {
            if (_pauseEvent == null) return;

            if (_isPaused)
            {
                _pauseEvent.Set(); // Dajemy zielone światło (Odmrażamy)
                _isPaused = false;
                btnPauza.Text = "Pauza";
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] ▶️ WZNOWIONO AUTOMATYZACJĘ.\n");
            }
            else
            {
                _pauseEvent.Reset(); // Zapalamy czerwone światło (Zamrażamy)
                _isPaused = true;
                btnPauza.Text = "Wznów";
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] ⏸️ ZAPAUZOWANO. Kliknij Wznów, aby kontynuować.\n");
            }
        }

        // ==========================================
        // OBSŁUGA PRZYCISKU STOP
        // ==========================================
        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // Wysyłamy sygnał przerwania (wyłapie go nasz 'AktywnySleep' i token)
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] 🛑 WYSŁANO SYGNAŁ PRZERWANIA PROCESU...\n");

                // Jeśli program spał w trybie pauzy, musimy go natychmiast "odmrozić", 
                // żeby pętla mogła wyłapać token anulowania i zakończyć proces
                if (_isPaused)
                {
                    _pauseEvent.Set();
                }
            }
        }

        private void txtSciezkaXml_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnWyczyscLogi_Click(object sender, EventArgs e)
        {
            rtbLogi.Clear();
        }

        private void btnZapiszLogi_Click(object sender, EventArgs e)
        {
            var linie = rtbLogi.Lines;

            EnovaAutomator.ZapiszLogiDoPliku(linie, (komunikat) =>
            {
                rtbLogi.AppendText(Environment.NewLine + komunikat);
            });
        }
    }
}