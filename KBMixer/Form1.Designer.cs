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
            appComboBox = new ComboBox();
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
            checkBoxSetAppManual = new CheckBox();
            textBoxAppManual = new TextBox();
            buttonSaveConfig = new Button();
            buttonRefresh = new Button();
            buttonDeleteConfig = new Button();
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
            deviceComboBox.Location = new Point(63, 33);
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
            // appComboBox
            // 
            appComboBox.AccessibleDescription = "App";
            appComboBox.AccessibleName = "App";
            appComboBox.AccessibleRole = AccessibleRole.ComboBox;
            appComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            appComboBox.FormattingEnabled = true;
            appComboBox.Location = new Point(63, 62);
            appComboBox.Name = "appComboBox";
            appComboBox.Size = new Size(172, 23);
            appComboBox.TabIndex = 2;
            appComboBox.UseWaitCursor = true;
            appComboBox.SelectedIndexChanged += appComboBox_SelectedIndexChanged;
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
            checkBoxControlSingleAppProcess.Location = new Point(163, 127);
            checkBoxControlSingleAppProcess.Name = "checkBoxControlSingleAppProcess";
            checkBoxControlSingleAppProcess.Size = new Size(15, 14);
            checkBoxControlSingleAppProcess.TabIndex = 8;
            checkBoxControlSingleAppProcess.UseVisualStyleBackColor = true;
            checkBoxControlSingleAppProcess.CheckedChanged += checkBoxControlSingleAppProcess_CheckedChanged;
            // 
            // processIndexSelector
            // 
            processIndexSelector.Enabled = false;
            processIndexSelector.Location = new Point(94, 152);
            processIndexSelector.Name = "processIndexSelector";
            processIndexSelector.Size = new Size(35, 23);
            processIndexSelector.TabIndex = 9;
            processIndexSelector.TextAlign = HorizontalAlignment.Center;
            // 
            // labelProcessIndex
            // 
            labelProcessIndex.AutoSize = true;
            labelProcessIndex.Location = new Point(7, 154);
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
            labelConfig.Click += labelConfig_Click;
            // 
            // comboBoxConfig
            // 
            comboBoxConfig.AccessibleDescription = "Device";
            comboBoxConfig.AccessibleName = "Device";
            comboBoxConfig.AccessibleRole = AccessibleRole.ComboBox;
            comboBoxConfig.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxConfig.FormattingEnabled = true;
            comboBoxConfig.Location = new Point(63, 4);
            comboBoxConfig.Name = "comboBoxConfig";
            comboBoxConfig.Size = new Size(275, 23);
            comboBoxConfig.TabIndex = 11;
            // 
            // labelHotkeys
            // 
            labelHotkeys.AutoSize = true;
            labelHotkeys.Location = new Point(7, 95);
            labelHotkeys.Name = "labelHotkeys";
            labelHotkeys.Size = new Size(50, 15);
            labelHotkeys.TabIndex = 13;
            labelHotkeys.Text = "Hotkeys";
            // 
            // textboxHotkeys
            // 
            textboxHotkeys.Location = new Point(63, 91);
            textboxHotkeys.Name = "textboxHotkeys";
            textboxHotkeys.ReadOnly = true;
            textboxHotkeys.Size = new Size(262, 23);
            textboxHotkeys.TabIndex = 14;
            textboxHotkeys.TextAlign = HorizontalAlignment.Center;
            // 
            // buttonHotkeyAdd
            // 
            buttonHotkeyAdd.Location = new Point(331, 91);
            buttonHotkeyAdd.Name = "buttonHotkeyAdd";
            buttonHotkeyAdd.Size = new Size(63, 23);
            buttonHotkeyAdd.TabIndex = 15;
            buttonHotkeyAdd.Text = "Add";
            buttonHotkeyAdd.UseVisualStyleBackColor = true;
            buttonHotkeyAdd.Click += buttonHotkeyAdd_Click;
            // 
            // buttonHotkeyReset
            // 
            buttonHotkeyReset.Location = new Point(400, 91);
            buttonHotkeyReset.Name = "buttonHotkeyReset";
            buttonHotkeyReset.Size = new Size(63, 23);
            buttonHotkeyReset.TabIndex = 16;
            buttonHotkeyReset.Text = "Reset";
            buttonHotkeyReset.UseVisualStyleBackColor = true;
            buttonHotkeyReset.Click += buttonHotkeyReset_Click;
            // 
            // labelControlSingleAppProcess
            // 
            labelControlSingleAppProcess.AutoSize = true;
            labelControlSingleAppProcess.Location = new Point(7, 126);
            labelControlSingleAppProcess.Name = "labelControlSingleAppProcess";
            labelControlSingleAppProcess.Size = new Size(150, 15);
            labelControlSingleAppProcess.TabIndex = 18;
            labelControlSingleAppProcess.Text = "Control Single App Process";
            // 
            // checkBoxSetAppManual
            // 
            checkBoxSetAppManual.AutoSize = true;
            checkBoxSetAppManual.Location = new Point(244, 64);
            checkBoxSetAppManual.Name = "checkBoxSetAppManual";
            checkBoxSetAppManual.Size = new Size(94, 19);
            checkBoxSetAppManual.TabIndex = 19;
            checkBoxSetAppManual.Text = "Set Manually";
            checkBoxSetAppManual.UseVisualStyleBackColor = true;
            checkBoxSetAppManual.CheckedChanged += checkBoxSetAppManual_CheckedChanged;
            // 
            // textBoxAppManual
            // 
            textBoxAppManual.Location = new Point(344, 62);
            textBoxAppManual.Name = "textBoxAppManual";
            textBoxAppManual.ReadOnly = true;
            textBoxAppManual.Size = new Size(119, 23);
            textBoxAppManual.TabIndex = 20;
            // 
            // buttonSaveConfig
            // 
            buttonSaveConfig.Location = new Point(344, 4);
            buttonSaveConfig.Name = "buttonSaveConfig";
            buttonSaveConfig.Size = new Size(57, 23);
            buttonSaveConfig.TabIndex = 21;
            buttonSaveConfig.Text = "Save";
            buttonSaveConfig.UseVisualStyleBackColor = true;
            // 
            // buttonRefresh
            // 
            buttonRefresh.Location = new Point(368, 33);
            buttonRefresh.Name = "buttonRefresh";
            buttonRefresh.Size = new Size(75, 23);
            buttonRefresh.TabIndex = 22;
            buttonRefresh.Text = "Refresh";
            buttonRefresh.UseVisualStyleBackColor = true;
            // 
            // buttonDeleteConfig
            // 
            buttonDeleteConfig.Location = new Point(407, 4);
            buttonDeleteConfig.Name = "buttonDeleteConfig";
            buttonDeleteConfig.Size = new Size(57, 23);
            buttonDeleteConfig.TabIndex = 23;
            buttonDeleteConfig.Text = "Delete";
            buttonDeleteConfig.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(476, 194);
            Controls.Add(buttonDeleteConfig);
            Controls.Add(buttonRefresh);
            Controls.Add(buttonSaveConfig);
            Controls.Add(textBoxAppManual);
            Controls.Add(checkBoxSetAppManual);
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
        private CheckBox checkBoxSetAppManual;
        private TextBox textBoxAppManual;
        private Button buttonSaveConfig;
        private Button buttonRefresh;
        private Button buttonDeleteConfig;
    }
}
