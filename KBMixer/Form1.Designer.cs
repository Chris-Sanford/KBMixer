namespace KBMixer
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
            appLabel = new Label();
            checkBoxControlSingleAppProcess = new CheckBox();
            processIndexSelector = new NumericUpDown();
            labelProcessIndex = new Label();
            labelConfig = new Label();
            comboBoxConfig = new ComboBox();
            labelHotkeys = new Label();
            textboxHotkeys = new TextBox();
            buttonHotkeyAdd = new Button();
            buttonHotkeyReset = new Button();
            labelControlSingleAppProcess = new Label();
            buttonNewConfig = new Button();
            buttonRefreshAudio = new Button();
            buttonDeleteConfig = new Button();
            textBoxAppSelected = new TextBox();
            buttonAppSet = new Button();
            labelInstructions = new Label();
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
            deviceComboBox.Location = new Point(56, 33);
            deviceComboBox.Name = "deviceComboBox";
            deviceComboBox.Size = new Size(275, 23);
            deviceComboBox.TabIndex = 0;
            deviceComboBox.SelectedIndexChanged += deviceComboBox_SelectedIndexChanged;
            // 
            // deviceLabel
            // 
            deviceLabel.AutoSize = true;
            deviceLabel.Location = new Point(7, 36);
            deviceLabel.Name = "deviceLabel";
            deviceLabel.Size = new Size(42, 15);
            deviceLabel.TabIndex = 1;
            deviceLabel.Text = "Device";
            // 
            // appLabel
            // 
            appLabel.AutoSize = true;
            appLabel.Location = new Point(7, 65);
            appLabel.Name = "appLabel";
            appLabel.Size = new Size(29, 15);
            appLabel.TabIndex = 3;
            appLabel.Text = "App";
            // 
            // checkBoxControlSingleAppProcess
            // 
            checkBoxControlSingleAppProcess.AutoSize = true;
            checkBoxControlSingleAppProcess.Location = new Point(229, 136);
            checkBoxControlSingleAppProcess.Name = "checkBoxControlSingleAppProcess";
            checkBoxControlSingleAppProcess.Size = new Size(15, 14);
            checkBoxControlSingleAppProcess.TabIndex = 8;
            checkBoxControlSingleAppProcess.UseVisualStyleBackColor = true;
            checkBoxControlSingleAppProcess.CheckedChanged += checkBoxControlSingleAppProcess_CheckedChanged;
            // 
            // processIndexSelector
            // 
            processIndexSelector.Enabled = false;
            processIndexSelector.Location = new Point(342, 133);
            processIndexSelector.Name = "processIndexSelector";
            processIndexSelector.Size = new Size(35, 23);
            processIndexSelector.TabIndex = 9;
            processIndexSelector.TextAlign = HorizontalAlignment.Center;
            processIndexSelector.ValueChanged += processIndexSelector_ValueChanged;
            // 
            // labelProcessIndex
            // 
            labelProcessIndex.AutoSize = true;
            labelProcessIndex.Location = new Point(255, 135);
            labelProcessIndex.Name = "labelProcessIndex";
            labelProcessIndex.Size = new Size(81, 15);
            labelProcessIndex.TabIndex = 10;
            labelProcessIndex.Text = "Process Index:";
            // 
            // labelConfig
            // 
            labelConfig.AutoSize = true;
            labelConfig.Location = new Point(7, 7);
            labelConfig.Name = "labelConfig";
            labelConfig.Size = new Size(43, 15);
            labelConfig.TabIndex = 12;
            labelConfig.Text = "Config";
            // 
            // comboBoxConfig
            // 
            comboBoxConfig.AccessibleDescription = "Device";
            comboBoxConfig.AccessibleName = "Device";
            comboBoxConfig.AccessibleRole = AccessibleRole.ComboBox;
            comboBoxConfig.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxConfig.FormattingEnabled = true;
            comboBoxConfig.Location = new Point(56, 4);
            comboBoxConfig.Name = "comboBoxConfig";
            comboBoxConfig.Size = new Size(275, 23);
            comboBoxConfig.TabIndex = 11;
            comboBoxConfig.SelectedIndexChanged += comboBoxConfig_SelectedIndexChanged;
            // 
            // labelHotkeys
            // 
            labelHotkeys.AutoSize = true;
            labelHotkeys.Location = new Point(7, 96);
            labelHotkeys.Name = "labelHotkeys";
            labelHotkeys.Size = new Size(50, 15);
            labelHotkeys.TabIndex = 13;
            labelHotkeys.Text = "Hotkeys";
            // 
            // textboxHotkeys
            // 
            textboxHotkeys.ImeMode = ImeMode.On;
            textboxHotkeys.Location = new Point(56, 93);
            textboxHotkeys.Name = "textboxHotkeys";
            textboxHotkeys.PlaceholderText = "Add hotkeys";
            textboxHotkeys.ReadOnly = true;
            textboxHotkeys.Size = new Size(275, 23);
            textboxHotkeys.TabIndex = 14;
            // 
            // buttonHotkeyAdd
            // 
            buttonHotkeyAdd.Location = new Point(337, 93);
            buttonHotkeyAdd.Name = "buttonHotkeyAdd";
            buttonHotkeyAdd.Size = new Size(65, 25);
            buttonHotkeyAdd.TabIndex = 15;
            buttonHotkeyAdd.Text = "Add";
            buttonHotkeyAdd.UseVisualStyleBackColor = true;
            buttonHotkeyAdd.Click += buttonHotkeyAdd_Click;
            // 
            // buttonHotkeyReset
            // 
            buttonHotkeyReset.Location = new Point(408, 93);
            buttonHotkeyReset.Name = "buttonHotkeyReset";
            buttonHotkeyReset.Size = new Size(65, 25);
            buttonHotkeyReset.TabIndex = 16;
            buttonHotkeyReset.Text = "Reset";
            buttonHotkeyReset.UseVisualStyleBackColor = true;
            buttonHotkeyReset.Click += buttonHotkeyReset_Click;
            // 
            // labelControlSingleAppProcess
            // 
            labelControlSingleAppProcess.AutoSize = true;
            labelControlSingleAppProcess.Location = new Point(73, 135);
            labelControlSingleAppProcess.Name = "labelControlSingleAppProcess";
            labelControlSingleAppProcess.Size = new Size(150, 15);
            labelControlSingleAppProcess.TabIndex = 18;
            labelControlSingleAppProcess.Text = "Control Single App Process";
            // 
            // buttonNewConfig
            // 
            buttonNewConfig.Location = new Point(337, 4);
            buttonNewConfig.Name = "buttonNewConfig";
            buttonNewConfig.Size = new Size(65, 25);
            buttonNewConfig.TabIndex = 21;
            buttonNewConfig.Text = "New";
            buttonNewConfig.UseVisualStyleBackColor = true;
            buttonNewConfig.Click += buttonNewConfig_Click;
            // 
            // buttonRefreshAudio
            // 
            buttonRefreshAudio.Location = new Point(369, 33);
            buttonRefreshAudio.Name = "buttonRefreshAudio";
            buttonRefreshAudio.Size = new Size(75, 23);
            buttonRefreshAudio.TabIndex = 22;
            buttonRefreshAudio.Text = "Refresh";
            buttonRefreshAudio.UseVisualStyleBackColor = true;
            // 
            // buttonDeleteConfig
            // 
            buttonDeleteConfig.Location = new Point(408, 4);
            buttonDeleteConfig.Name = "buttonDeleteConfig";
            buttonDeleteConfig.Size = new Size(65, 25);
            buttonDeleteConfig.TabIndex = 23;
            buttonDeleteConfig.Text = "Delete";
            buttonDeleteConfig.UseVisualStyleBackColor = true;
            buttonDeleteConfig.Click += buttonDeleteConfig_Click;
            // 
            // textBoxAppSelected
            // 
            textBoxAppSelected.Location = new Point(56, 62);
            textBoxAppSelected.Name = "textBoxAppSelected";
            textBoxAppSelected.PlaceholderText = "Select an app";
            textBoxAppSelected.ReadOnly = true;
            textBoxAppSelected.Size = new Size(275, 23);
            textBoxAppSelected.TabIndex = 24;
            // 
            // buttonAppSet
            // 
            buttonAppSet.Location = new Point(369, 62);
            buttonAppSet.Name = "buttonAppSet";
            buttonAppSet.Size = new Size(75, 23);
            buttonAppSet.TabIndex = 26;
            buttonAppSet.Text = "Set";
            buttonAppSet.UseVisualStyleBackColor = true;
            buttonAppSet.Click += buttonAppSet_Click;
            // 
            // labelInstructions
            // 
            labelInstructions.AutoSize = true;
            labelInstructions.Location = new Point(56, 171);
            labelInstructions.Name = "labelInstructions";
            labelInstructions.Size = new Size(366, 45);
            labelInstructions.TabIndex = 27;
            labelInstructions.Text = "Once you've configured KBMixer as desired,\nhold the required hotkeys and scroll up or down with your mouse to\nadjust the volume of the selected app from anywhere.";
            labelInstructions.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(482, 225);
            Controls.Add(labelInstructions);
            Controls.Add(buttonAppSet);
            Controls.Add(textBoxAppSelected);
            Controls.Add(buttonDeleteConfig);
            Controls.Add(buttonRefreshAudio);
            Controls.Add(buttonNewConfig);
            Controls.Add(labelControlSingleAppProcess);
            Controls.Add(buttonHotkeyReset);
            Controls.Add(buttonHotkeyAdd);
            Controls.Add(textboxHotkeys);
            Controls.Add(labelHotkeys);
            Controls.Add(labelConfig);
            Controls.Add(comboBoxConfig);
            Controls.Add(labelProcessIndex);
            Controls.Add(processIndexSelector);
            Controls.Add(checkBoxControlSingleAppProcess);
            Controls.Add(appLabel);
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
        private Label appLabel;
        private CheckBox checkBoxControlSingleAppProcess;
        private NumericUpDown processIndexSelector;
        private Label labelProcessIndex;
        private Label labelConfig;
        private ComboBox comboBoxConfig;
        private Label labelHotkeys;
        private TextBox textboxHotkeys;
        private Button buttonHotkeyAdd;
        private Button buttonHotkeyReset;
        private Label labelControlSingleAppProcess;
        private Button buttonNewConfig;
        private Button buttonRefreshAudio;
        private Button buttonDeleteConfig;
        private TextBox textBoxAppSelected;
        private Button buttonAppSet;
        private Label labelInstructions;
    }
}
