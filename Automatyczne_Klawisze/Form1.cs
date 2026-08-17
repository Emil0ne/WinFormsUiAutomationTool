using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            // DOMYŚLNE WARTOŚCI PÓL FORMULARZA
            // ==========================================
            txtSciezkaEnova.Text = @"C:\Program Files (x86)\Soneta\enova365 2512.9.11\SonetaExplorer.exe";
            txtEnovaUser.Text = "Administrator";
            txtNowyOperator.Text = "Test";
            txtNoweHaslo.Text = "test";
            txtSciezkaXml.Text = @"C:\Users\Administrator.OFFICE\Desktop\OPERATORZY.xml";

            // Domyślne wartości dla SQL Server
            txtSqlServer.Text = @"PMKADRYPLACE\SQLEXPRESS";
            txtSqlLogin.Text = "sa";
            txtSqlHaslo.Text = "MS76PMk@dry2023";

            // Domyślnie zaznaczamy pierwszą opcję dla bezpieczeństwa
            rbUzgodnijRole.Checked = true;
            AktualizujDostepnoscPolSql();
        }

        private void ZastosujCiemnyMotyw()
        {
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.FromArgb(240, 240, 240);

            foreach (Control ctrl in this.Controls)
            {
                ZastosujKoloryDlaKontrolki(ctrl);
            }
        }

        private void ZastosujKoloryDlaKontrolki(Control ctrl)
        {
            if (ctrl is TextBox || ctrl is RichTextBox || ctrl is CheckedListBox)
            {
                ctrl.BackColor = Color.FromArgb(45, 45, 48);
                ctrl.ForeColor = Color.FromArgb(240, 240, 240);
            }
            else if (ctrl is Button)
            {
                ctrl.BackColor = Color.FromArgb(60, 60, 65);
                ctrl.ForeColor = Color.FromArgb(255, 255, 255);
                ((Button)ctrl).FlatStyle = FlatStyle.Flat;
                ((Button)ctrl).FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            }
            else if (ctrl is Label || ctrl is RadioButton || ctrl is GroupBox)
            {
                ctrl.ForeColor = Color.FromArgb(240, 240, 240);
                ctrl.BackColor = Color.Transparent;
            }
            else if (ctrl is TabPage)
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

            if (e.State == DrawItemState.Selected)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 48)), e.Bounds);
            }
            else
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(32, 32, 32)), e.Bounds);
            }

            StringFormat stringFlags = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            Brush textBrush = (e.State == DrawItemState.Selected) ? new SolidBrush(Color.White) : new SolidBrush(Color.FromArgb(150, 150, 150));
            g.DrawString(tabPage.Text, e.Font, textBrush, tabBounds, stringFlags);
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
                        clbBazy.Items.Clear();
                        string wybranaSciezka = openFileDialog.FileName;

                        List<string> bazy = EnovaConfigReader.PobierzBazyZXml(wybranaSciezka);

                        if (bazy.Count > 0)
                        {
                            foreach (string baza in bazy)
                            {
                                clbBazy.Items.Add(baza, true); // domyślnie zaznaczamy wszystkie
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

        private string PobierzWersjeEnovaZPliku(string sciezkaExe)
        {
            if (string.IsNullOrWhiteSpace(sciezkaExe) || !File.Exists(sciezkaExe))
                return "";

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(sciezkaExe);
                if (!string.IsNullOrWhiteSpace(versionInfo.ProductVersion))
                {
                    var matchProd = Regex.Match(versionInfo.ProductVersion, @"\d+(\.\d+)+");
                    if (matchProd.Success) return matchProd.Value;
                }

                if (!string.IsNullOrWhiteSpace(versionInfo.FileVersion))
                {
                    var matchFile = Regex.Match(versionInfo.FileVersion, @"\d+(\.\d+)+");
                    if (matchFile.Success) return matchFile.Value;
                }
            }
            catch { }

            var matchFolder = Regex.Match(sciezkaExe, @"(\d{4}\.\d+\.\d+)");
            return matchFolder.Success ? matchFolder.Value : "";
        }

        private async Task<bool> FiltrujBazyWedlugWersjiAsync(string docelowaWersja)
        {
            // Pobieramy indeksy tylko tych baz, które użytkownik zaznaczył
            var zaznaczoneIndeksy = clbBazy.CheckedIndices.Cast<int>().ToList();

            if (zaznaczoneIndeksy.Count == 0)
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] ⚠️ Musisz zaznaczyć przynajmniej jedną bazę na liście!\n");
                return false;
            }

            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🔍 Weryfikacja {zaznaczoneIndeksy.Count} zaznaczonych baz w SQL (wersja docelowa: {docelowaWersja})...\n");

            string connStr = SqlVersionChecker.ZbudujConnectionString(txtSqlServer.Text, txtSqlLogin.Text, txtSqlHaslo.Text);

            try
            {
                var mapyWersji = await SqlVersionChecker.PobierzMapyWersjiAsync(connStr);
                int doAktualizacji = 0;

                // Iterujemy WYŁĄCZNIE po bazach zaznaczonych przez użytkownika
                foreach (int i in zaznaczoneIndeksy)
                {
                    string nazwaBazy = clbBazy.Items[i].ToString().Trim();

                    string klucz = mapyWersji.Keys.FirstOrDefault(k =>
                        k.Equals(nazwaBazy, StringComparison.OrdinalIgnoreCase) ||
                        k.Replace("_", " ").Equals(nazwaBazy.Replace("_", " "), StringComparison.OrdinalIgnoreCase) ||
                        k.Equals(nazwaBazy.Replace(" ", "_"), StringComparison.OrdinalIgnoreCase));

                    if (klucz != null && mapyWersji.TryGetValue(klucz, out var info))
                    {
                        if (info.CzyPoprawnaEnova)
                        {
                            int porownanie = SqlVersionChecker.PorownajWersje(info.Wersja, docelowaWersja);

                            if (porownanie < 0)
                            {
                                // Baza jest starsza -> ZOSTAW PZAZNACZONĄ
                                clbBazy.SetItemChecked(i, true);
                                rtbLogi.AppendText($"  [+] {nazwaBazy}: {info.Wersja} < {docelowaWersja} -> POZOSTAWIONO DO AKTUALIZACJI\n");
                                doAktualizacji++;
                            }
                            else if (porownanie == 0)
                            {
                                // Baza jest aktualna -> ODZNACZAMY
                                clbBazy.SetItemChecked(i, false);
                                rtbLogi.AppendText($"  [-] {nazwaBazy}: {info.Wersja} == {docelowaWersja} -> ODZNACZONO (aktualna)\n");
                            }
                            else
                            {
                                // Baza jest nowsza -> ODZNACZAMY
                                clbBazy.SetItemChecked(i, false);
                                rtbLogi.AppendText($"  [-] {nazwaBazy}: {info.Wersja} > {docelowaWersja} -> ODZNACZONO (nowsza)\n");
                            }
                        }
                        else
                        {
                            clbBazy.SetItemChecked(i, false);
                            rtbLogi.AppendText($"  [-] {nazwaBazy}: {info.Wersja} -> ODZNACZONO (brak wpisu Enova)\n");
                        }
                    }
                    else
                    {
                        clbBazy.SetItemChecked(i, false);
                        rtbLogi.AppendText($"  [-] {nazwaBazy}: Nie odnaleziono bazy w instancji SQL -> ODZNACZONO\n");
                    }
                }

                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] 🏁 Wynik: Pozostawiono {doAktualizacji} baz wymagających aktualizacji.\n");
                return doAktualizacji > 0;
            }
            catch (Exception ex)
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] ❌ BŁĄD SQL: {ex.Message}\n");
                return false;
            }
        }

        private async void btnAktualizuj_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSciezkaEnova.Text) || !File.Exists(txtSciezkaEnova.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Nie wybrano prawidłowego pliku uruchomieniowego Enova (.exe)!\n");
                return;
            }

            // 1. Walidacja zaznaczenia przed jakimkolwiek zapytaniem
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

            string docelowaWersja = PobierzWersjeEnovaZPliku(txtSciezkaEnova.Text);
            if (string.IsNullOrEmpty(docelowaWersja))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] ❌ BŁĄD: Nie udało się odczytać wersji pliku wykonywalnego Enovy.\n");
                return;
            }

            rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] 📌 Docelowa wersja Enovy: {docelowaWersja}\n");

            // 2. Filtrujemy TYLKO zaznaczone pozycje
            bool saBazyDoZrobienia = await FiltrujBazyWedlugWersjiAsync(docelowaWersja);
            if (!saBazyDoZrobienia)
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] Żadna z zaznaczonych baz nie wymaga aktualizacji do wersji {docelowaWersja}.\n");
                return;
            }

            // 3. Pobieramy pozostałe po weryfikacji zaznaczone bazy
            List<string> bazyDoPrzetworzenia = new List<string>();
            foreach (var item in clbBazy.CheckedItems)
            {
                bazyDoPrzetworzenia.Add(item.ToString());
            }

            string login = txtEnovaUser.Text;
            string haslo = txtEnovaPass.Text;
            string sciezkaEnova = txtSciezkaEnova.Text;

            int lacznieBaz = bazyDoPrzetworzenia.Count;
            int zrobione = 0;
            progressBar1.Maximum = lacznieBaz;
            progressBar1.Value = 0;
            lblPostep.Text = $"Postęp: 0 / {lacznieBaz}";

            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🔄 START AKTUALIZACJI / KONWERSJI DLA {lacznieBaz} BAZ!\n");

            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true);
            _isPaused = false;
            btnPauza.Text = "Pauza";

            Task.Run(() =>
            {
                EnovaAktualizacja.Uruchom(bazyDoPrzetworzenia, login, haslo, sciezkaEnova, _cts.Token, _pauseEvent,
                (wiadomosc) =>
                {
                    Invoke(new Action(() =>
                    {
                        rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] {wiadomosc}\n");
                        rtbLogi.SelectionStart = rtbLogi.Text.Length;
                        rtbLogi.ScrollToCaret();
                    }));
                },
                (nazwaBazyZakonczonej) =>
                {
                    Invoke(new Action(() =>
                    {
                        int idx = clbBazy.Items.IndexOf(nazwaBazyZakonczonej);
                        if (idx >= 0) clbBazy.SetItemChecked(idx, false);

                        zrobione++;
                        if (zrobione <= progressBar1.Maximum) progressBar1.Value = zrobione;
                        lblPostep.Text = $"Postęp: {zrobione} / {lacznieBaz}";
                    }));
                });
            });
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

            int lacznieBaz = bazyDoPrzetworzenia.Count;
            int zrobione = 0;
            progressBar1.Maximum = lacznieBaz;
            progressBar1.Value = 0;
            lblPostep.Text = $"Postęp: 0 / {lacznieBaz}";

            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🚀 START AUTOMATYZACJI [OPERATORZY]! Bazy: {lacznieBaz}\n");

            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true);
            _isPaused = false;
            btnPauza.Text = "Pauza";

            Task.Run(() =>
            {
                EnovaOperatorzy.Uruchom(bazyDoPrzetworzenia, login, haslo, nowyOp, hasloOp, sciezkaXml, sciezkaEnova, _cts.Token, _pauseEvent,
                (wiadomosc) =>
                {
                    Invoke(new Action(() =>
                    {
                        rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] {wiadomosc}\n");
                        rtbLogi.SelectionStart = rtbLogi.Text.Length;
                        rtbLogi.ScrollToCaret();
                    }));
                },
                (nazwaBazyZakonczonej) =>
                {
                    Invoke(new Action(() =>
                    {
                        int idx = clbBazy.Items.IndexOf(nazwaBazyZakonczonej);
                        if (idx >= 0) clbBazy.SetItemChecked(idx, false);

                        zrobione++;
                        if (zrobione <= progressBar1.Maximum) progressBar1.Value = zrobione;
                        lblPostep.Text = $"Postęp: {zrobione} / {lacznieBaz}";
                    }));
                });
            });
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

        private void btnPauza_Click(object sender, EventArgs e)
        {
            if (_pauseEvent == null) return;

            if (_isPaused)
            {
                _pauseEvent.Set();
                _isPaused = false;
                btnPauza.Text = "Pauza";
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] ▶️ WZNOWIONO AUTOMATYZACJĘ.\n");
            }
            else
            {
                _pauseEvent.Reset();
                _isPaused = true;
                btnPauza.Text = "Wznów";
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] ⏸️ ZAPAUZOWANO. Kliknij Wznów, aby kontynuować.\n");
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] 🛑 WYSŁANO SYGNAŁ PRZERWANIA PROCESU...\n");

                if (_isPaused)
                {
                    _pauseEvent.Set();
                }
            }
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

        private void btnSprawdzSystemPraw_Click(object sender, EventArgs e)
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

            int lacznieBaz = bazyDoPrzetworzenia.Count;
            int zrobione = 0;
            progressBar1.Maximum = lacznieBaz;
            progressBar1.Value = 0;
            lblPostep.Text = $"Postęp: 0 / {lacznieBaz}";

            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🔍 START SPRAWDZANIA SYSTEMU PRAW! Bazy: {lacznieBaz}\n");

            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true);
            _isPaused = false;
            btnPauza.Text = "Pauza";

            Task.Run(() =>
            {
                EnovaSystemPraw.Uruchom(bazyDoPrzetworzenia, login, haslo, sciezkaEnova, _cts.Token, _pauseEvent,
                (wiadomosc) =>
                {
                    Invoke(new Action(() =>
                    {
                        rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] {wiadomosc}\n");
                        rtbLogi.SelectionStart = rtbLogi.Text.Length;
                        rtbLogi.ScrollToCaret();
                    }));
                },
                (nazwaBazyZakonczonej) =>
                {
                    Invoke(new Action(() =>
                    {
                        int idx = clbBazy.Items.IndexOf(nazwaBazyZakonczonej);
                        if (idx >= 0) clbBazy.SetItemChecked(idx, false);

                        zrobione++;
                        if (zrobione <= progressBar1.Maximum) progressBar1.Value = zrobione;
                        lblPostep.Text = $"Postęp: {zrobione} / {lacznieBaz}";
                    }));
                });
            });
        }

        private void btnKonwersjaPraw_Click(object sender, EventArgs e)
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

            if (!rbUzgodnijRole.Checked && !rbPelnaKonwersja.Checked)
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Wybierz opcję konwersji ról!\n");
                return;
            }

            bool tylkoUzgodnijRole = rbUzgodnijRole.Checked;
            string sqlConnString = "";

            if (!tylkoUzgodnijRole)
            {
                if (string.IsNullOrWhiteSpace(txtSqlServer.Text))
                {
                    rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Podaj serwer SQL!\n");
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtSqlLogin.Text))
                {
                    rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Podaj login do serwera SQL!\n");
                    return;
                }
                sqlConnString = SqlChecker.ZbudujConnectionString(txtSqlServer.Text, txtSqlLogin.Text, txtSqlHaslo.Text);
            }

            List<string> bazyDoPrzetworzenia = new List<string>();
            foreach (var item in clbBazy.CheckedItems)
            {
                bazyDoPrzetworzenia.Add(item.ToString());
            }

            string login = txtEnovaUser.Text;
            string haslo = txtEnovaPass.Text;
            string sciezkaEnova = txtSciezkaEnova.Text;

            int lacznieBaz = bazyDoPrzetworzenia.Count;
            int zrobione = 0;
            progressBar1.Maximum = lacznieBaz;
            progressBar1.Value = 0;
            lblPostep.Text = $"Postęp: 0 / {lacznieBaz}";

            string opcjaTxt = tylkoUzgodnijRole ? "TYLKO UZGODNIENIE RÓL" : "PEŁNA KONWERSJA (ROZSZERZONY)";
            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🔄 START KONWERSJI SYSTEMU PRAW [{opcjaTxt}]! Bazy: {lacznieBaz}\n");

            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true);
            _isPaused = false;
            btnPauza.Text = "Pauza";

            Task.Run(() =>
            {
                EnovaKonwersjaPraw.Uruchom(
                    bazyDoPrzetworzenia,
                    login,
                    haslo,
                    sciezkaEnova,
                    tylkoUzgodnijRole,
                    sqlConnString,
                    _cts.Token,
                    _pauseEvent,
                    (wiadomosc) =>
                    {
                        Invoke(new Action(() =>
                        {
                            rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] {wiadomosc}\n");
                            rtbLogi.SelectionStart = rtbLogi.Text.Length;
                            rtbLogi.ScrollToCaret();
                        }));
                    },
                    (nazwaBazyZakonczonej) =>
                    {
                        Invoke(new Action(() =>
                        {
                            int idx = clbBazy.Items.IndexOf(nazwaBazyZakonczonej);
                            if (idx >= 0)
                            {
                                clbBazy.SetItemChecked(idx, false);
                            }

                            zrobione++;
                            if (zrobione <= progressBar1.Maximum)
                            {
                                progressBar1.Value = zrobione;
                            }
                            lblPostep.Text = $"Postęp: {zrobione} / {lacznieBaz}";
                        }));
                    });
            });
        }

        private void AktualizujDostepnoscPolSql()
        {
            bool pelna = rbPelnaKonwersja.Checked;
            txtSqlServer.Enabled = pelna;
            txtSqlLogin.Enabled = pelna;
            txtSqlHaslo.Enabled = pelna;
        }

        private void rbPelnaKonwersja_CheckedChanged(object sender, EventArgs e)
        {
            AktualizujDostepnoscPolSql();
        }

        private void rbUzgodnijRole_CheckedChanged(object sender, EventArgs e)
        {
            AktualizujDostepnoscPolSql();
        }

        private void label2_Click(object sender, EventArgs e) { }
        private void label5_Click(object sender, EventArgs e) { }
        private void clbBazy_SelectedIndexChanged(object sender, EventArgs e) { }
        private void rtbLogi_TextChanged(object sender, EventArgs e) { }
        private void txtSciezkaXml_TextChanged(object sender, EventArgs e) { }
        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void txtSqlServer_TextChanged(object sender, EventArgs e) { }
        private void txtSqlLogin_TextChanged(object sender, EventArgs e) { }
        private void txtSqlHaslo_TextChanged(object sender, EventArgs e) { }
        private void Form1_Load(object sender, EventArgs e) { }
        private void lblPostep_Click(object sender, EventArgs e) { }
    }
}