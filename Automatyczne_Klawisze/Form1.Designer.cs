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
            SuspendLayout();
            // 
            // btnWczytajBazy
            // 
            btnWczytajBazy.Location = new Point(16, 13);
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
            clbBazy.Location = new Point(16, 78);
            clbBazy.Name = "clbBazy";
            clbBazy.Size = new Size(238, 148);
            clbBazy.TabIndex = 1;
            clbBazy.SelectedIndexChanged += clbBazy_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(16, 299);
            label1.Name = "label1";
            label1.Size = new Size(92, 15);
            label1.TabIndex = 2;
            label1.Text = "Login do Enovy:";
            // 
            // txtEnovaUser
            // 
            txtEnovaUser.Location = new Point(114, 296);
            txtEnovaUser.Name = "txtEnovaUser";
            txtEnovaUser.Size = new Size(140, 23);
            txtEnovaUser.TabIndex = 3;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(16, 328);
            label2.Name = "label2";
            label2.Size = new Size(92, 15);
            label2.TabIndex = 4;
            label2.Text = "Hasło do Enovy:";
            label2.Click += label2_Click;
            // 
            // txtEnovaPass
            // 
            txtEnovaPass.Location = new Point(114, 325);
            txtEnovaPass.Name = "txtEnovaPass";
            txtEnovaPass.Size = new Size(140, 23);
            txtEnovaPass.TabIndex = 5;
            // 
            // txtNoweHaslo
            // 
            txtNoweHaslo.Location = new Point(114, 383);
            txtNoweHaslo.Name = "txtNoweHaslo";
            txtNoweHaslo.Size = new Size(140, 23);
            txtNoweHaslo.TabIndex = 9;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(16, 386);
            label3.Name = "label3";
            label3.Size = new Size(94, 15);
            label3.TabIndex = 8;
            label3.Text = "Hasło operatora:";
            // 
            // txtNowyOperator
            // 
            txtNowyOperator.Location = new Point(114, 354);
            txtNowyOperator.Name = "txtNowyOperator";
            txtNowyOperator.Size = new Size(140, 23);
            txtNowyOperator.TabIndex = 7;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(16, 357);
            label4.Name = "label4";
            label4.Size = new Size(85, 15);
            label4.TabIndex = 6;
            label4.Text = "Kod operatora:";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(16, 415);
            label5.Name = "label5";
            label5.Size = new Size(85, 15);
            label5.TabIndex = 10;
            label5.Text = "XML z danymi:";
            label5.Click += label5_Click;
            // 
            // txtSciezkaXml
            // 
            txtSciezkaXml.Location = new Point(16, 441);
            txtSciezkaXml.Name = "txtSciezkaXml";
            txtSciezkaXml.Size = new Size(238, 23);
            txtSciezkaXml.TabIndex = 11;
            // 
            // btnWybierzXml
            // 
            btnWybierzXml.Location = new Point(114, 412);
            btnWybierzXml.Name = "btnWybierzXml";
            btnWybierzXml.Size = new Size(140, 23);
            btnWybierzXml.TabIndex = 12;
            btnWybierzXml.Text = "...";
            btnWybierzXml.UseVisualStyleBackColor = true;
            btnWybierzXml.Click += btnWybierzXml_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(316, 525);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(232, 23);
            btnStart.TabIndex = 13;
            btnStart.Text = "URUCHOM DODAWANIE OPERATORÓW";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // rtbLogi
            // 
            rtbLogi.Location = new Point(602, 12);
            rtbLogi.Name = "rtbLogi";
            rtbLogi.ReadOnly = true;
            rtbLogi.Size = new Size(317, 300);
            rtbLogi.TabIndex = 14;
            rtbLogi.Text = "";
            rtbLogi.TextChanged += rtbLogi_TextChanged;
            // 
            // btnZaznaczWszystko
            // 
            btnZaznaczWszystko.Location = new Point(16, 42);
            btnZaznaczWszystko.Name = "btnZaznaczWszystko";
            btnZaznaczWszystko.Size = new Size(116, 23);
            btnZaznaczWszystko.TabIndex = 15;
            btnZaznaczWszystko.Text = "Zaznacz wszystko";
            btnZaznaczWszystko.UseVisualStyleBackColor = true;
            btnZaznaczWszystko.Click += btnZaznaczWszystko_Click;
            // 
            // btnOdznaczWszystko
            // 
            btnOdznaczWszystko.Location = new Point(138, 42);
            btnOdznaczWszystko.Name = "btnOdznaczWszystko";
            btnOdznaczWszystko.Size = new Size(116, 23);
            btnOdznaczWszystko.TabIndex = 16;
            btnOdznaczWszystko.Text = "Odznacz wszystko";
            btnOdznaczWszystko.UseVisualStyleBackColor = true;
            btnOdznaczWszystko.Click += btnOdznaczWszystko_Click;
            // 
            // btnWybierzEnova
            // 
            btnWybierzEnova.Location = new Point(151, 235);
            btnWybierzEnova.Name = "btnWybierzEnova";
            btnWybierzEnova.Size = new Size(103, 23);
            btnWybierzEnova.TabIndex = 19;
            btnWybierzEnova.Text = "...";
            btnWybierzEnova.UseVisualStyleBackColor = true;
            btnWybierzEnova.Click += btnWybierzEnova_Click;
            // 
            // txtSciezkaEnova
            // 
            txtSciezkaEnova.Location = new Point(16, 264);
            txtSciezkaEnova.Name = "txtSciezkaEnova";
            txtSciezkaEnova.Size = new Size(238, 23);
            txtSciezkaEnova.TabIndex = 18;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(16, 238);
            label6.Name = "label6";
            label6.Size = new Size(132, 15);
            label6.TabIndex = 17;
            label6.Text = "Ścieżka do Enovy (.exe):";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(931, 560);
            Controls.Add(btnWybierzEnova);
            Controls.Add(txtSciezkaEnova);
            Controls.Add(label6);
            Controls.Add(btnOdznaczWszystko);
            Controls.Add(btnZaznaczWszystko);
            Controls.Add(rtbLogi);
            Controls.Add(btnStart);
            Controls.Add(btnWybierzXml);
            Controls.Add(txtSciezkaXml);
            Controls.Add(label5);
            Controls.Add(txtNoweHaslo);
            Controls.Add(label3);
            Controls.Add(txtNowyOperator);
            Controls.Add(label4);
            Controls.Add(txtEnovaPass);
            Controls.Add(label2);
            Controls.Add(txtEnovaUser);
            Controls.Add(label1);
            Controls.Add(clbBazy);
            Controls.Add(btnWczytajBazy);
            Name = "Form1";
            Text = "Form1";
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
    }
}
