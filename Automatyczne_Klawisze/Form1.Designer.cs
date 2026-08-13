namespace Automatyczne_Klawisze
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnWczytajBazy = new Button();
            clbBazy = new CheckedListBox();
            label1 = new Label();
            txtEnovaUser = new TextBox();
            label2 = new Label();
            txtEnovaPass = new TextBox();
            txtNoweHaslo = new TextBox();
            label3 = new Label();
            txtNowyOperator = new TextBox();
            label4 = new Label();
            label5 = new Label();
            txtSciezkaXml = new TextBox();
            btnWybierzXml = new Button();
            btnStart = new Button();
            rtbLogi = new RichTextBox();
            btnZaznaczWszystko = new Button();
            btnOdznaczWszystko = new Button();
            btnWybierzEnova = new Button();
            txtSciezkaEnova = new TextBox();
            label6 = new Label();
            btnStop = new Button();
            btnPauza = new Button();
            btnWyczyscLogi = new Button();
            btnZapiszLogi = new Button();
            tabControl1 = new TabControl();
            dodanieOperatora = new TabPage();
            zmianaWersji = new TabPage();
            btnAktualizuj = new Button();
            systemPraw = new TabPage();
            btnSprawdzSystemPraw = new Button();
            konwersjaPraw = new TabPage();
            textBox1 = new TextBox();
            rbPelnaKonwersja = new RadioButton();
            rbUzgodnijRole = new RadioButton();
            btnKonwersjaPraw = new Button();
            tabControl1.SuspendLayout();
            dodanieOperatora.SuspendLayout();
            zmianaWersji.SuspendLayout();
            systemPraw.SuspendLayout();
            konwersjaPraw.SuspendLayout();
            SuspendLayout();
            // 
            // btnWczytajBazy
            // 
            btnWczytajBazy.Location = new Point(12, 129);
            btnWczytajBazy.Name = "btnWczytajBazy";
            btnWczytajBazy.Size = new Size(116, 23);
            btnWczytajBazy.TabIndex = 0;
            btnWczytajBazy.Text = "1. Wczytaj listę baz";
            btnWczytajBazy.UseVisualStyleBackColor = true;
            btnWczytajBazy.Click += btnWczytajBazy_Click;
            // 
            // clbBazy
            // 
            clbBazy.FormattingEnabled = true;
            clbBazy.Location = new Point(12, 194);
            clbBazy.Name = "clbBazy";
            clbBazy.Size = new Size(238, 112);
            clbBazy.TabIndex = 1;
            clbBazy.SelectedIndexChanged += clbBazy_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 74);
            label1.Name = "label1";
            label1.Size = new Size(92, 15);
            label1.TabIndex = 2;
            label1.Text = "Login do Enovy:";
            // 
            // txtEnovaUser
            // 
            txtEnovaUser.Location = new Point(110, 71);
            txtEnovaUser.Name = "txtEnovaUser";
            txtEnovaUser.Size = new Size(140, 23);
            txtEnovaUser.TabIndex = 3;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 103);
            label2.Name = "label2";
            label2.Size = new Size(92, 15);
            label2.TabIndex = 4;
            label2.Text = "Hasło do Enovy:";
            label2.Click += label2_Click;
            // 
            // txtEnovaPass
            // 
            txtEnovaPass.Location = new Point(110, 100);
            txtEnovaPass.Name = "txtEnovaPass";
            txtEnovaPass.Size = new Size(140, 23);
            txtEnovaPass.TabIndex = 5;
            // 
            // txtNoweHaslo
            // 
            txtNoweHaslo.Location = new Point(104, 39);
            txtNoweHaslo.Name = "txtNoweHaslo";
            txtNoweHaslo.Size = new Size(140, 23);
            txtNoweHaslo.TabIndex = 9;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 42);
            label3.Name = "label3";
            label3.Size = new Size(94, 15);
            label3.TabIndex = 8;
            label3.Text = "Hasło operatora:";
            // 
            // txtNowyOperator
            // 
            txtNowyOperator.Location = new Point(104, 10);
            txtNowyOperator.Name = "txtNowyOperator";
            txtNowyOperator.Size = new Size(140, 23);
            txtNowyOperator.TabIndex = 7;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(6, 13);
            label4.Name = "label4";
            label4.Size = new Size(94, 15);
            label4.TabIndex = 6;
            label4.Text = "Login operatora:";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(6, 71);
            label5.Name = "label5";
            label5.Size = new Size(85, 15);
            label5.TabIndex = 10;
            label5.Text = "XML z danymi:";
            label5.Click += label5_Click;
            // 
            // txtSciezkaXml
            // 
            txtSciezkaXml.Location = new Point(6, 97);
            txtSciezkaXml.Name = "txtSciezkaXml";
            txtSciezkaXml.Size = new Size(238, 23);
            txtSciezkaXml.TabIndex = 11;
            txtSciezkaXml.TextChanged += txtSciezkaXml_TextChanged;
            // 
            // btnWybierzXml
            // 
            btnWybierzXml.Location = new Point(104, 68);
            btnWybierzXml.Name = "btnWybierzXml";
            btnWybierzXml.Size = new Size(140, 23);
            btnWybierzXml.TabIndex = 12;
            btnWybierzXml.Text = "...";
            btnWybierzXml.UseVisualStyleBackColor = true;
            btnWybierzXml.Click += btnWybierzXml_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(6, 126);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(232, 23);
            btnStart.TabIndex = 13;
            btnStart.Text = "URUCHOM DODAWANIE OPERATORÓW";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // rtbLogi
            // 
            rtbLogi.Location = new Point(604, 13);
            rtbLogi.Name = "rtbLogi";
            rtbLogi.ReadOnly = true;
            rtbLogi.Size = new Size(317, 422);
            rtbLogi.TabIndex = 14;
            rtbLogi.Text = "";
            rtbLogi.TextChanged += rtbLogi_TextChanged;
            // 
            // btnZaznaczWszystko
            // 
            btnZaznaczWszystko.Location = new Point(12, 158);
            btnZaznaczWszystko.Name = "btnZaznaczWszystko";
            btnZaznaczWszystko.Size = new Size(116, 23);
            btnZaznaczWszystko.TabIndex = 15;
            btnZaznaczWszystko.Text = "Zaznacz wszystko";
            btnZaznaczWszystko.UseVisualStyleBackColor = true;
            btnZaznaczWszystko.Click += btnZaznaczWszystko_Click;
            // 
            // btnOdznaczWszystko
            // 
            btnOdznaczWszystko.Location = new Point(134, 158);
            btnOdznaczWszystko.Name = "btnOdznaczWszystko";
            btnOdznaczWszystko.Size = new Size(116, 23);
            btnOdznaczWszystko.TabIndex = 16;
            btnOdznaczWszystko.Text = "Odznacz wszystko";
            btnOdznaczWszystko.UseVisualStyleBackColor = true;
            btnOdznaczWszystko.Click += btnOdznaczWszystko_Click;
            // 
            // btnWybierzEnova
            // 
            btnWybierzEnova.Location = new Point(147, 10);
            btnWybierzEnova.Name = "btnWybierzEnova";
            btnWybierzEnova.Size = new Size(103, 23);
            btnWybierzEnova.TabIndex = 19;
            btnWybierzEnova.Text = "...";
            btnWybierzEnova.UseVisualStyleBackColor = true;
            btnWybierzEnova.Click += btnWybierzEnova_Click;
            // 
            // txtSciezkaEnova
            // 
            txtSciezkaEnova.Location = new Point(12, 39);
            txtSciezkaEnova.Name = "txtSciezkaEnova";
            txtSciezkaEnova.Size = new Size(238, 23);
            txtSciezkaEnova.TabIndex = 18;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(12, 13);
            label6.Name = "label6";
            label6.Size = new Size(132, 15);
            label6.TabIndex = 17;
            label6.Text = "Ścieżka do Enovy (.exe):";
            // 
            // btnStop
            // 
            btnStop.Location = new Point(491, 525);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(113, 23);
            btnStop.TabIndex = 20;
            btnStop.Text = "STOP";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // btnPauza
            // 
            btnPauza.Location = new Point(357, 525);
            btnPauza.Name = "btnPauza";
            btnPauza.Size = new Size(128, 23);
            btnPauza.TabIndex = 21;
            btnPauza.Text = "PAUZA";
            btnPauza.UseVisualStyleBackColor = true;
            btnPauza.Click += btnPauza_Click;
            // 
            // btnWyczyscLogi
            // 
            btnWyczyscLogi.Location = new Point(604, 441);
            btnWyczyscLogi.Name = "btnWyczyscLogi";
            btnWyczyscLogi.Size = new Size(140, 23);
            btnWyczyscLogi.TabIndex = 22;
            btnWyczyscLogi.Text = "Wyczyść logi";
            btnWyczyscLogi.UseVisualStyleBackColor = true;
            btnWyczyscLogi.Click += btnWyczyscLogi_Click;
            // 
            // btnZapiszLogi
            // 
            btnZapiszLogi.Location = new Point(781, 441);
            btnZapiszLogi.Name = "btnZapiszLogi";
            btnZapiszLogi.Size = new Size(140, 23);
            btnZapiszLogi.TabIndex = 23;
            btnZapiszLogi.Text = "Zapisz logi do TXT";
            btnZapiszLogi.UseVisualStyleBackColor = true;
            btnZapiszLogi.Click += btnZapiszLogi_Click;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(dodanieOperatora);
            tabControl1.Controls.Add(zmianaWersji);
            tabControl1.Controls.Add(systemPraw);
            tabControl1.Controls.Add(konwersjaPraw);
            tabControl1.Location = new Point(12, 312);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(373, 207);
            tabControl1.TabIndex = 24;
            // 
            // dodanieOperatora
            // 
            dodanieOperatora.Controls.Add(label4);
            dodanieOperatora.Controls.Add(txtNowyOperator);
            dodanieOperatora.Controls.Add(label3);
            dodanieOperatora.Controls.Add(txtNoweHaslo);
            dodanieOperatora.Controls.Add(label5);
            dodanieOperatora.Controls.Add(txtSciezkaXml);
            dodanieOperatora.Controls.Add(btnWybierzXml);
            dodanieOperatora.Controls.Add(btnStart);
            dodanieOperatora.Location = new Point(4, 24);
            dodanieOperatora.Name = "dodanieOperatora";
            dodanieOperatora.Padding = new Padding(3);
            dodanieOperatora.Size = new Size(365, 179);
            dodanieOperatora.TabIndex = 0;
            dodanieOperatora.Text = "Dodanie operatora";
            dodanieOperatora.UseVisualStyleBackColor = true;
            // 
            // zmianaWersji
            // 
            zmianaWersji.Controls.Add(btnAktualizuj);
            zmianaWersji.Location = new Point(4, 24);
            zmianaWersji.Name = "zmianaWersji";
            zmianaWersji.Padding = new Padding(3);
            zmianaWersji.Size = new Size(365, 179);
            zmianaWersji.TabIndex = 1;
            zmianaWersji.Text = "Zmiana wersji";
            zmianaWersji.UseVisualStyleBackColor = true;
            // 
            // btnAktualizuj
            // 
            btnAktualizuj.Location = new Point(51, 76);
            btnAktualizuj.Name = "btnAktualizuj";
            btnAktualizuj.Size = new Size(232, 23);
            btnAktualizuj.TabIndex = 14;
            btnAktualizuj.Text = "URUCHOM AKTUALIZACJĘ BAZ";
            btnAktualizuj.UseVisualStyleBackColor = true;
            btnAktualizuj.Click += btnAktualizuj_Click;
            // 
            // systemPraw
            // 
            systemPraw.Controls.Add(btnSprawdzSystemPraw);
            systemPraw.Location = new Point(4, 24);
            systemPraw.Name = "systemPraw";
            systemPraw.Size = new Size(365, 179);
            systemPraw.TabIndex = 2;
            systemPraw.Text = "System praw";
            systemPraw.UseVisualStyleBackColor = true;
            // 
            // btnSprawdzSystemPraw
            // 
            btnSprawdzSystemPraw.Location = new Point(52, 78);
            btnSprawdzSystemPraw.Name = "btnSprawdzSystemPraw";
            btnSprawdzSystemPraw.Size = new Size(232, 23);
            btnSprawdzSystemPraw.TabIndex = 15;
            btnSprawdzSystemPraw.Text = "SPRAWDŹ SYSTEM PRAW";
            btnSprawdzSystemPraw.UseVisualStyleBackColor = true;
            btnSprawdzSystemPraw.Click += btnSprawdzSystemPraw_Click;
            // 
            // konwersjaPraw
            // 
            konwersjaPraw.Controls.Add(textBox1);
            konwersjaPraw.Controls.Add(rbPelnaKonwersja);
            konwersjaPraw.Controls.Add(rbUzgodnijRole);
            konwersjaPraw.Controls.Add(btnKonwersjaPraw);
            konwersjaPraw.Location = new Point(4, 24);
            konwersjaPraw.Name = "konwersjaPraw";
            konwersjaPraw.Size = new Size(365, 179);
            konwersjaPraw.TabIndex = 3;
            konwersjaPraw.Text = "Konwersja praw";
            konwersjaPraw.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(131, 12);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(100, 23);
            textBox1.TabIndex = 19;
            textBox1.Text = "Co chcesz zrobić?";
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // rbPelnaKonwersja
            // 
            rbPelnaKonwersja.AutoSize = true;
            rbPelnaKonwersja.Location = new Point(197, 41);
            rbPelnaKonwersja.Name = "rbPelnaKonwersja";
            rbPelnaKonwersja.Size = new Size(110, 19);
            rbPelnaKonwersja.TabIndex = 18;
            rbPelnaKonwersja.Text = "Pełna konwersja";
            rbPelnaKonwersja.UseVisualStyleBackColor = true;
            rbPelnaKonwersja.CheckedChanged += rbPelnaKonwersja_CheckedChanged;
            // 
            // rbUzgodnijRole
            // 
            rbUzgodnijRole.AutoSize = true;
            rbUzgodnijRole.Checked = true;
            rbUzgodnijRole.Location = new Point(59, 41);
            rbUzgodnijRole.Name = "rbUzgodnijRole";
            rbUzgodnijRole.Size = new Size(95, 19);
            rbUzgodnijRole.TabIndex = 17;
            rbUzgodnijRole.TabStop = true;
            rbUzgodnijRole.Text = "Uzgodnij role";
            rbUzgodnijRole.UseVisualStyleBackColor = true;
            rbUzgodnijRole.CheckedChanged += rbUzgodnijRole_CheckedChanged;
            // 
            // btnKonwersjaPraw
            // 
            btnKonwersjaPraw.Location = new Point(59, 105);
            btnKonwersjaPraw.Name = "btnKonwersjaPraw";
            btnKonwersjaPraw.Size = new Size(232, 23);
            btnKonwersjaPraw.TabIndex = 16;
            btnKonwersjaPraw.Text = "ZMIEŃ SYSTEM PRAW";
            btnKonwersjaPraw.UseVisualStyleBackColor = true;
            btnKonwersjaPraw.Click += btnKonwersjaPraw_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(944, 560);
            Controls.Add(btnWczytajBazy);
            Controls.Add(tabControl1);
            Controls.Add(clbBazy);
            Controls.Add(btnZapiszLogi);
            Controls.Add(label1);
            Controls.Add(btnOdznaczWszystko);
            Controls.Add(btnWyczyscLogi);
            Controls.Add(txtEnovaUser);
            Controls.Add(btnZaznaczWszystko);
            Controls.Add(btnPauza);
            Controls.Add(label2);
            Controls.Add(btnStop);
            Controls.Add(btnWybierzEnova);
            Controls.Add(txtEnovaPass);
            Controls.Add(rtbLogi);
            Controls.Add(txtSciezkaEnova);
            Controls.Add(label6);
            Name = "Form1";
            Text = "Form1";
            tabControl1.ResumeLayout(false);
            dodanieOperatora.ResumeLayout(false);
            dodanieOperatora.PerformLayout();
            zmianaWersji.ResumeLayout(false);
            systemPraw.ResumeLayout(false);
            konwersjaPraw.ResumeLayout(false);
            konwersjaPraw.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnWczytajBazy;
        private CheckedListBox clbBazy;
        private Label label1;
        private TextBox txtEnovaUser;
        private Label label2;
        private TextBox txtEnovaPass;
        private TextBox txtNoweHaslo;
        private Label label3;
        private TextBox txtNowyOperator;
        private Label label4;
        private Label label5;
        private TextBox txtSciezkaXml;
        private Button btnWybierzXml;
        private Button btnStart;
        private RichTextBox rtbLogi;
        private Button btnZaznaczWszystko;
        private Button btnOdznaczWszystko;
        private Button btnWybierzEnova;
        private TextBox txtSciezkaEnova;
        private Label label6;
        private Button btnStop;
        private Button btnPauza;
        private Button btnWyczyscLogi;
        private Button btnZapiszLogi;
        private TabControl tabControl1;
        private TabPage dodanieOperatora;
        private TabPage zmianaWersji;
        private Button btnAktualizuj;
        private TabPage systemPraw;
        private Button btnSprawdzSystemPraw;
        private TabPage konwersjaPraw;
        private Button btnKonwersjaPraw;
        private TextBox textBox1;
        private RadioButton rbPelnaKonwersja;
        private RadioButton rbUzgodnijRole;
    }
}
