namespace KBMixerWinForm
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
            deviceComboBox = new ComboBox();
            deviceLabel = new Label();
            appComboBox = new ComboBox();
            appLabel = new Label();
            SuspendLayout();
            // 
            // deviceComboBox
            // 
            deviceComboBox.AccessibleDescription = "Device";
            deviceComboBox.AccessibleName = "Device";
            deviceComboBox.AccessibleRole = AccessibleRole.ComboBox;
            deviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            deviceComboBox.FormattingEnabled = true;
            deviceComboBox.Location = new Point(60, 6);
            deviceComboBox.Name = "deviceComboBox";
            deviceComboBox.Size = new Size(423, 23);
            deviceComboBox.TabIndex = 0;
            deviceComboBox.SelectedIndexChanged += deviceComboBox_SelectedIndexChanged;
            // 
            // deviceLabel
            // 
            deviceLabel.AutoSize = true;
            deviceLabel.Location = new Point(12, 9);
            deviceLabel.Name = "deviceLabel";
            deviceLabel.Size = new Size(42, 15);
            deviceLabel.TabIndex = 1;
            deviceLabel.Text = "Device";
            deviceLabel.Click += label1_Click;
            // 
            // appComboBox
            // 
            appComboBox.AccessibleDescription = "App";
            appComboBox.AccessibleName = "App";
            appComboBox.AccessibleRole = AccessibleRole.ComboBox;
            appComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            appComboBox.FormattingEnabled = true;
            appComboBox.Location = new Point(60, 35);
            appComboBox.Name = "appComboBox";
            appComboBox.Size = new Size(423, 23);
            appComboBox.TabIndex = 2;
            // 
            // appLabel
            // 
            appLabel.AutoSize = true;
            appLabel.Location = new Point(12, 38);
            appLabel.Name = "appLabel";
            appLabel.Size = new Size(29, 15);
            appLabel.TabIndex = 3;
            appLabel.Text = "App";
            appLabel.Click += label2_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(appLabel);
            Controls.Add(appComboBox);
            Controls.Add(deviceLabel);
            Controls.Add(deviceComboBox);
            Name = "Form1";
            Text = "KBMixer";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox deviceComboBox;
        private Label deviceLabel;
        private ComboBox appComboBox;
        private Label appLabel;
    }
}
