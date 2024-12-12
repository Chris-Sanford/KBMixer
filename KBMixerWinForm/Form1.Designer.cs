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
            VolumeUp = new Button();
            VolumeDown = new Button();
            btnHotkey = new Button();
            checkBoxControlSingleAppProcess = new CheckBox();
            processIndexSelector = new NumericUpDown();
            labelProcessIndex = new Label();
            ((System.ComponentModel.ISupportInitialize)processIndexSelector).BeginInit();
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
            deviceComboBox.Size = new Size(339, 23);
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
            appComboBox.Size = new Size(339, 23);
            appComboBox.TabIndex = 2;
            appComboBox.SelectedIndexChanged += appComboBox_SelectedIndexChanged;
            // 
            // appLabel
            // 
            appLabel.AutoSize = true;
            appLabel.Location = new Point(12, 38);
            appLabel.Name = "appLabel";
            appLabel.Size = new Size(29, 15);
            appLabel.TabIndex = 3;
            appLabel.Text = "App";
            // 
            // VolumeUp
            // 
            VolumeUp.Location = new Point(278, 64);
            VolumeUp.Name = "VolumeUp";
            VolumeUp.Size = new Size(88, 23);
            VolumeUp.TabIndex = 4;
            VolumeUp.Text = "VolumeUp";
            VolumeUp.UseVisualStyleBackColor = true;
            VolumeUp.Click += VolumeUpButton_Click;
            // 
            // VolumeDown
            // 
            VolumeDown.Location = new Point(278, 93);
            VolumeDown.Name = "VolumeDown";
            VolumeDown.Size = new Size(88, 23);
            VolumeDown.TabIndex = 5;
            VolumeDown.Text = "VolumeDown";
            VolumeDown.UseVisualStyleBackColor = true;
            VolumeDown.Click += VolumeDownButton_Click;
            // 
            // btnHotkey
            // 
            btnHotkey.Location = new Point(60, 64);
            btnHotkey.Name = "btnHotkey";
            btnHotkey.Size = new Size(125, 23);
            btnHotkey.TabIndex = 6;
            btnHotkey.Text = "Set Hotkey";
            btnHotkey.UseVisualStyleBackColor = true;
            btnHotkey.Click += btnHotkey_Click;
            // 
            // checkBoxControlSingleAppProcess
            // 
            checkBoxControlSingleAppProcess.AutoSize = true;
            checkBoxControlSingleAppProcess.Location = new Point(60, 93);
            checkBoxControlSingleAppProcess.Name = "checkBoxControlSingleAppProcess";
            checkBoxControlSingleAppProcess.Size = new Size(169, 19);
            checkBoxControlSingleAppProcess.TabIndex = 8;
            checkBoxControlSingleAppProcess.Text = "Control Single App Process";
            checkBoxControlSingleAppProcess.UseVisualStyleBackColor = true;
            // 
            // processIndexSelector
            // 
            processIndexSelector.Location = new Point(144, 118);
            processIndexSelector.Name = "processIndexSelector";
            processIndexSelector.Size = new Size(35, 23);
            processIndexSelector.TabIndex = 9;
            processIndexSelector.TextAlign = HorizontalAlignment.Center;
            // 
            // labelProcessIndex
            // 
            labelProcessIndex.AutoSize = true;
            labelProcessIndex.Location = new Point(57, 120);
            labelProcessIndex.Name = "labelProcessIndex";
            labelProcessIndex.Size = new Size(81, 15);
            labelProcessIndex.TabIndex = 10;
            labelProcessIndex.Text = "Process Index:";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(420, 175);
            Controls.Add(labelProcessIndex);
            Controls.Add(processIndexSelector);
            Controls.Add(checkBoxControlSingleAppProcess);
            Controls.Add(btnHotkey);
            Controls.Add(VolumeDown);
            Controls.Add(VolumeUp);
            Controls.Add(appLabel);
            Controls.Add(appComboBox);
            Controls.Add(deviceLabel);
            Controls.Add(deviceComboBox);
            Name = "Form1";
            Text = "KBMixer";
            ((System.ComponentModel.ISupportInitialize)processIndexSelector).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox deviceComboBox;
        private Label deviceLabel;
        private ComboBox appComboBox;
        private Label appLabel;
        private Button VolumeUp;
        private Button VolumeDown;
        private Button btnHotkey;
        private CheckBox checkBoxControlSingleAppProcess;
        private NumericUpDown processIndexSelector;
        private Label labelProcessIndex;
    }
}
