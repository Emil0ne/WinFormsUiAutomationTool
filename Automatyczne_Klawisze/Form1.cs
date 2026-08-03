namespace Automatyczne_Klawisze
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnWczytajBazy_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                openFileDialog.InitialDirectory = System.IO.Path.Combine(appData, "Soneta");
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
            if (string.IsNullOrWhiteSpace(txtSciezkaEnova.Text) || !System.IO.File.Exists(txtSciezkaEnova.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Nie wybrano prawidłowego pliku uruchomieniowego Enova (.exe)!\n");
                return;
            }

            if (clbBazy.CheckedItems.Count == 0)
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Musisz zaznaczyć przynajmniej jedną bazę na liście!\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtEnovaUser.Text) || string.IsNullOrWhiteSpace(txtEnovaPass.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Wprowadź login i hasło do Enovy!\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtNowyOperator.Text) || string.IsNullOrWhiteSpace(txtNoweHaslo.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Podaj kod i hasło dla nowego operatora!\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtSciezkaXml.Text) || !System.IO.File.Exists(txtSciezkaXml.Text))
            {
                rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] BŁĄD: Nie wybrano prawidłowego pliku XML do importu!\n");
                return;
            }

            List<string> bazyDoPrzetworzenia = new List<string>();
            foreach (var item in clbBazy.CheckedItems)
            {
                bazyDoPrzetworzenia.Add(item.ToString()!);
            }

            string login = txtEnovaUser.Text;
            string haslo = txtEnovaPass.Text;
            string nowyOp = txtNowyOperator.Text;
            string hasloOp = txtNoweHaslo.Text;
            string sciezkaXml = txtSciezkaXml.Text;
            string sciezkaEnova = txtSciezkaEnova.Text; 

            rtbLogi.AppendText($"\n[{DateTime.Now:HH:mm:ss}] 🚀 START AUTOMATYZACJI! Bazy do przetworzenia: {bazyDoPrzetworzenia.Count}\n");

            System.Threading.Tasks.Task.Run(() =>
            {
                EnovaAutomator.Uruchom(bazyDoPrzetworzenia, login, haslo, nowyOp, hasloOp, sciezkaXml, sciezkaEnova, (wiadomosc) =>
                {
                    Invoke(new Action(() => rtbLogi.AppendText($"[{DateTime.Now:HH:mm:ss}] {wiadomosc}\n")));

                    Invoke(new Action(() => {
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
    }
}
