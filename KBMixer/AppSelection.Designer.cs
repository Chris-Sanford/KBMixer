namespace KBMixer
{
    partial class AppSelection
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            radioSelect = new RadioButton();
            radioEnter = new RadioButton();
            comboBoxSelect = new ComboBox();
            textBoxEnter = new TextBox();
            buttonOk = new Button();
            buttonCancel = new Button();
            SuspendLayout();
            // 
            // radioSelect
            // 
            radioSelect.AutoSize = true;
            radioSelect.Location = new Point(12, 12);
            radioSelect.Name = "radioSelect";
            radioSelect.Size = new Size(186, 19);
            radioSelect.TabIndex = 0;
            radioSelect.TabStop = true;
            radioSelect.Text = "Select from List of Active Apps";
            radioSelect.UseVisualStyleBackColor = true;
            radioSelect.CheckedChanged += radioSelect_CheckedChanged;
            // 
            // radioEnter
            // 
            radioEnter.AutoSize = true;
            radioEnter.Location = new Point(12, 45);
            radioEnter.Name = "radioEnter";
            radioEnter.Size = new Size(164, 19);
            radioEnter.TabIndex = 1;
            radioEnter.TabStop = true;
            radioEnter.Text = "Enter App Name Manually";
            radioEnter.UseVisualStyleBackColor = true;
            radioEnter.CheckedChanged += radioEnter_CheckedChanged;
            // 
            // comboBoxSelect
            // 
            comboBoxSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSelect.FormattingEnabled = true;
            comboBoxSelect.Location = new Point(204, 12);
            comboBoxSelect.Name = "comboBoxSelect";
            comboBoxSelect.Size = new Size(203, 23);
            comboBoxSelect.TabIndex = 2;
            // 
            // textBoxEnter
            // 
            textBoxEnter.Location = new Point(204, 41);
            textBoxEnter.Name = "textBoxEnter";
            textBoxEnter.Size = new Size(203, 23);
            textBoxEnter.TabIndex = 3;
            // 
            // buttonOk
            // 
            buttonOk.Location = new Point(123, 89);
            buttonOk.Name = "buttonOk";
            buttonOk.Size = new Size(75, 23);
            buttonOk.TabIndex = 4;
            buttonOk.Text = "OK";
            buttonOk.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(204, 89);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 23);
            buttonCancel.TabIndex = 5;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            // 
            // AppSelection
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(423, 134);
            Controls.Add(buttonCancel);
            Controls.Add(buttonOk);
            Controls.Add(textBoxEnter);
            Controls.Add(comboBoxSelect);
            Controls.Add(radioEnter);
            Controls.Add(radioSelect);
            Name = "AppSelection";
            Text = "Select an Application";
            ResumeLayout(false);
            PerformLayout();
        }

        private RadioButton radioSelect;
        private RadioButton radioEnter;
        private ComboBox comboBoxSelect;
        private TextBox textBoxEnter;
        private Button buttonOk;
        private Button buttonCancel;
    }
}