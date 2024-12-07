namespace RawGlobalInputGUI01
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
            richTextBox1 = new RichTextBox();
            btnHotkey = new Button();
            SuspendLayout();
            // 
            // richTextBox1
            // 
            richTextBox1.Location = new Point(12, 12);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(218, 270);
            richTextBox1.TabIndex = 0;
            richTextBox1.Text = "";
            // 
            // btnHotkey
            // 
            btnHotkey.Location = new Point(268, 12);
            btnHotkey.Name = "btnHotkey";
            btnHotkey.Size = new Size(133, 23);
            btnHotkey.TabIndex = 1;
            btnHotkey.Text = "Set Hotkey";
            btnHotkey.UseVisualStyleBackColor = true;
            btnHotkey.Click += btnHotkey_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(431, 294);
            Controls.Add(btnHotkey);
            Controls.Add(richTextBox1);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion

        private RichTextBox richTextBox1;
        private Button btnHotkey;
    }
}
