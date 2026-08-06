using System;
using System.Collections.Generic;
using System.Drawing;
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
            tabControl1.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl1.DrawItem += tabControl1_DrawItem;
            // --- NAKŁADANIE CZYSTEGO, CIEMNEGO MOTYWU ---
            ZastosujCiemnyMotyw();

            // ==========================================
            // DOMYŚLNE WARTOŚCI POL FORMULARZA
            // ==========================================
            txtSciezkaEnova.Text = @"C:\Program Files (x86)\Soneta\enova365 2512.9.11\SonetaExplorer.exe";
            txtEnovaUser.Text = "Administrator";
            txtNowyOperator.Text = "Test";
            txtNoweHaslo.Text = "test";
            txtSciezkaXml.Text = @"C:\Users\Administrator.OFFICE\Desktop\OPERATORZY.xml";
        }

        private void ZastosujCiemnyMotyw()
        {
            // Kolor tła głównego okna oraz tekst domyślny
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.FromArgb(240, 240, 240);

            // Rekurencyjne barwienie wszystkich kontrolek w oknie
            foreach (Control ctrl in this.Controls)
            {
                ZastosujKoloryDlaKontrolki(ctrl);
            }
        }

        private void ZastosujKoloryDlaKontrolki(Control ctrl)
        {
            if (ctrl is TextBox || ctrl is RichTextBox || ctrl is CheckedListBox)
            {
                ctrl.BackColor = Color.FromArgb(45, 45, 48); // Ciemny grafit
                ctrl.ForeColor = Color.FromArgb(240, 240, 240); // Jasny tekst
            }
            else if (ctrl is Button)
            {
                ctrl.BackColor = Color.FromArgb(60, 60, 65);
                ctrl.ForeColor = Color.FromArgb(255, 255, 255);
                ((Button)ctrl).FlatStyle = FlatStyle.Flat; // Spłaszcza obramowanie
                ((Button)ctrl).FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            }
            else if (ctrl is Label)
            {
                ctrl.ForeColor = Color.FromArgb(240, 240, 240);
                ctrl.BackColor = Color.Transparent;
            }
            else if (ctrl is TabPage) // <--- DODANY WARUNEK DLA ZAKŁADEK
            {
                ctrl.BackColor = Color.FromArgb(32, 32, 32);
                ctrl.ForeColor = Color.FromArgb(240, 240, 240);
            }

            if (ctrl.HasChildren)
            {
                foreach (Control child in ctrl.Controls)
                {
                    ZastosujKoloryDlaKontrolki(child);
                }
            }
        }

        private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            TabPage tabPage = tabControl1.TabPages[e.Index];
            Rectangle tabBounds = tabControl1.GetTabRect(e.Index);

            // Tło zakładki w zależności od tego, czy jest aktywna
            if (e.State == DrawItemState.Selected)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 48)), e.Bounds); // Jaśniejsza dla aktywnej
            }
            else
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(32, 32, 32)), e.Bounds); // Ciemniejsza dla nieaktywnej
            }

            // Centrowanie i rysowanie tekstu
            StringFormat stringFlags = new StringFormat();
            stringFlags.Alignment = StringAlignment.Center;
            stringFlags.LineAlignment = StringAlignment.Center;

            Brush textBrush = (e.State == DrawItemState.Selected) ? new SolidBrush(Color.White) : new SolidBrush(Color.FromArgb(150, 150, 150));

            g.DrawString(tabPage.Text, e.Font, textBrush, tabBounds, new StringFormat(stringFlags));
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

            btnPauza.Text = "Pauza";

            Task.Run(() =>
            {
                EnovaOperatorzy.Uruchom(bazyDoPrzetworzenia, login, haslo, nowyOp, hasloOp, sciezkaXml, sciezkaEnova, _cts.Token, _pauseEvent, (wiadomosc) =>
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
                _cts.Cancel(); // Wysyłamy sygnał przerwania
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] 🛑 WYSŁANO SYGNAŁ PRZERWANIA PROCESU...\n");

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

            EnovaOperatorzy.ZapiszLogiDoPliku(linie, (komunikat) =>
            {
                rtbLogi.AppendText(Environment.NewLine + komunikat);
            });
        }

        private void btnAktualizuj_Click(object sender, EventArgs e)
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

            List<string> bazyDoPrzetworzenia = new List<string>();
            foreach (var item in clbBazy.CheckedItems)
            {
                bazyDoPrzetworzenia.Add(item.ToString());
            }

            string login = txtEnovaUser.Text;
            string haslo = txtEnovaPass.Text;
            string sciezkaEnova = txtSciezkaEnova.Text;

            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🔄 START AKTUALIZACJI / KONWERSJI! Bazy do przetworzenia: {bazyDoPrzetworzenia.Count}\n");

            // ==========================================
            // INICJALIZACJA KONTROLERÓW WĄTKU
            // ==========================================
            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true);
            _isPaused = false;
            btnPauza.Text = "Pauza";

            Task.Run(() =>
            {
                EnovaAktualizacja.Uruchom(bazyDoPrzetworzenia, login, haslo, sciezkaEnova, _cts.Token, _pauseEvent, (wiadomosc) =>
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
    }
}