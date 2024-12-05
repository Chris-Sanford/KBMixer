namespace Global_Input
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
            lastKeyPressed = new Label();
            MouseWheelActivity = new Label();
            SuspendLayout();
            // 
            // lastKeyPressed
            // 
            lastKeyPressed.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lastKeyPressed.AutoSize = true;
            lastKeyPressed.Location = new Point(46, 9);
            lastKeyPressed.Name = "lastKeyPressed";
            lastKeyPressed.Size = new Size(98, 15);
            lastKeyPressed.TabIndex = 0;
            lastKeyPressed.Text = "NoKeyPressedYet";
            lastKeyPressed.TextAlign = ContentAlignment.MiddleCenter;
            lastKeyPressed.Click += lastKeyPressed_Click;
            // 
            // MouseWheelActivity
            // 
            MouseWheelActivity.AutoSize = true;
            MouseWheelActivity.Location = new Point(32, 41);
            MouseWheelActivity.Name = "MouseWheelActivity";
            MouseWheelActivity.Size = new Size(132, 15);
            MouseWheelActivity.TabIndex = 1;
            MouseWheelActivity.Text = "NoMouseWheelActivity";
            MouseWheelActivity.Click += MouseWheelActivity_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(193, 76);
            Controls.Add(MouseWheelActivity);
            Controls.Add(lastKeyPressed);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lastKeyPressed;
        private Label MouseWheelActivity;
    }
}
