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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            deviceComboBox = new ComboBox();
            deviceLabel = new Label();
            appLabel = new Label();
            checkBoxControlSingleAppProcess = new CheckBox();
            comboBoxAudioSession = new ComboBox();
            labelProcessIndex = new Label();
            labelConfig = new Label();
            labelConfigDisplayName = new Label();
            textBoxConfigDisplayName = new TextBox();
            buttonResetDisplayName = new Button();
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
            trayIcon = new NotifyIcon(components);
            SuspendLayout();
            // 
            // deviceComboBox
            // 
            deviceComboBox.AccessibleDescription = "Device";
            deviceComboBox.AccessibleName = "Device";
            deviceComboBox.AccessibleRole = AccessibleRole.ComboBox;
            deviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            deviceComboBox.FormattingEnabled = true;
            deviceComboBox.Location = new Point(56, 62);
            deviceComboBox.Name = "deviceComboBox";
            deviceComboBox.Size = new Size(392, 23);
            deviceComboBox.TabIndex = 0;
            deviceComboBox.SelectedIndexChanged += deviceComboBox_SelectedIndexChanged;
            // 
            // deviceLabel
            // 
            deviceLabel.AutoSize = true;
            deviceLabel.Location = new Point(7, 65);
            deviceLabel.Name = "deviceLabel";
            deviceLabel.Size = new Size(42, 15);
            deviceLabel.TabIndex = 1;
            deviceLabel.Text = "Device";
            // 
            // appLabel
            // 
            appLabel.AutoSize = true;
            appLabel.Location = new Point(7, 94);
            appLabel.Name = "appLabel";
            appLabel.Size = new Size(29, 15);
            appLabel.TabIndex = 3;
            appLabel.Text = "App";
            // 
            // checkBoxControlSingleAppProcess
            // 
            checkBoxControlSingleAppProcess.AutoSize = true;
            checkBoxControlSingleAppProcess.Location = new Point(229, 165);
            checkBoxControlSingleAppProcess.Name = "checkBoxControlSingleAppProcess";
            checkBoxControlSingleAppProcess.Size = new Size(15, 14);
            checkBoxControlSingleAppProcess.TabIndex = 8;
            checkBoxControlSingleAppProcess.UseVisualStyleBackColor = true;
            checkBoxControlSingleAppProcess.CheckedChanged += checkBoxControlSingleAppProcess_CheckedChanged;
            // 
            // comboBoxAudioSession
            // 
            comboBoxAudioSession.AccessibleDescription = "Pick which audio session to control when using single-session mode";
            comboBoxAudioSession.AccessibleName = "Audio session";
            comboBoxAudioSession.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxAudioSession.Enabled = false;
            comboBoxAudioSession.FormattingEnabled = true;
            comboBoxAudioSession.IntegralHeight = false;
            comboBoxAudioSession.Location = new Point(342, 162);
            comboBoxAudioSession.Name = "comboBoxAudioSession";
            comboBoxAudioSession.Size = new Size(220, 23);
            comboBoxAudioSession.TabIndex = 9;
            comboBoxAudioSession.SelectedIndexChanged += comboBoxAudioSession_SelectedIndexChanged;
            // 
            // labelProcessIndex
            // 
            labelProcessIndex.AutoSize = true;
            labelProcessIndex.Location = new Point(255, 164);
            labelProcessIndex.Name = "labelProcessIndex";
            labelProcessIndex.Size = new Size(52, 15);
            labelProcessIndex.TabIndex = 10;
            labelProcessIndex.Text = "Session:";
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
            // labelConfigDisplayName
            // 
            labelConfigDisplayName.AutoSize = true;
            labelConfigDisplayName.Location = new Point(7, 36);
            labelConfigDisplayName.Name = "labelConfigDisplayName";
            labelConfigDisplayName.Size = new Size(39, 15);
            labelConfigDisplayName.TabIndex = 28;
            labelConfigDisplayName.Text = "Name";
            // 
            // textBoxConfigDisplayName
            // 
            textBoxConfigDisplayName.AccessibleDescription = "Configuration display name";
            textBoxConfigDisplayName.AccessibleName = "Configuration display name";
            textBoxConfigDisplayName.Location = new Point(56, 33);
            textBoxConfigDisplayName.Name = "textBoxConfigDisplayName";
            textBoxConfigDisplayName.PlaceholderText = "";
            textBoxConfigDisplayName.Size = new Size(392, 23);
            textBoxConfigDisplayName.TabIndex = 29;
            textBoxConfigDisplayName.Leave += textBoxConfigDisplayName_Leave;
            // 
            // buttonResetDisplayName
            // 
            buttonResetDisplayName.AccessibleDescription = "Clear custom name and use the default naming pattern";
            buttonResetDisplayName.AccessibleName = "Reset to default display name";
            buttonResetDisplayName.Location = new Point(456, 33);
            buttonResetDisplayName.Name = "buttonResetDisplayName";
            buttonResetDisplayName.Size = new Size(134, 23);
            buttonResetDisplayName.TabIndex = 30;
            buttonResetDisplayName.Text = "Reset to Default";
            buttonResetDisplayName.UseVisualStyleBackColor = true;
            buttonResetDisplayName.Click += buttonResetDisplayName_Click;
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
            comboBoxConfig.Size = new Size(392, 23);
            comboBoxConfig.TabIndex = 11;
            comboBoxConfig.SelectedIndexChanged += comboBoxConfig_SelectedIndexChanged;
            // 
            // labelHotkeys
            // 
            labelHotkeys.AutoSize = true;
            labelHotkeys.Location = new Point(7, 125);
            labelHotkeys.Name = "labelHotkeys";
            labelHotkeys.Size = new Size(50, 15);
            labelHotkeys.TabIndex = 13;
            labelHotkeys.Text = "Hotkeys";
            // 
            // textboxHotkeys
            // 
            textboxHotkeys.ImeMode = ImeMode.On;
            textboxHotkeys.Location = new Point(56, 122);
            textboxHotkeys.Name = "textboxHotkeys";
            textboxHotkeys.PlaceholderText = "Add hotkeys";
            textboxHotkeys.ReadOnly = true;
            textboxHotkeys.Size = new Size(392, 23);
            textboxHotkeys.TabIndex = 14;
            // 
            // buttonHotkeyAdd
            // 
            buttonHotkeyAdd.Location = new Point(456, 122);
            buttonHotkeyAdd.Name = "buttonHotkeyAdd";
            buttonHotkeyAdd.Size = new Size(64, 23);
            buttonHotkeyAdd.TabIndex = 15;
            buttonHotkeyAdd.Text = "Add";
            buttonHotkeyAdd.UseVisualStyleBackColor = true;
            buttonHotkeyAdd.Click += buttonHotkeyAdd_Click;
            // 
            // buttonHotkeyReset
            // 
            buttonHotkeyReset.Location = new Point(526, 122);
            buttonHotkeyReset.Name = "buttonHotkeyReset";
            buttonHotkeyReset.Size = new Size(64, 23);
            buttonHotkeyReset.TabIndex = 16;
            buttonHotkeyReset.Text = "Reset";
            buttonHotkeyReset.UseVisualStyleBackColor = true;
            buttonHotkeyReset.Click += buttonHotkeyReset_Click;
            // 
            // labelControlSingleAppProcess
            // 
            labelControlSingleAppProcess.AutoSize = true;
            labelControlSingleAppProcess.Location = new Point(73, 164);
            labelControlSingleAppProcess.Name = "labelControlSingleAppProcess";
            labelControlSingleAppProcess.Size = new Size(150, 15);
            labelControlSingleAppProcess.TabIndex = 18;
            labelControlSingleAppProcess.Text = "Control Single App Process";
            // 
            // buttonNewConfig
            // 
            buttonNewConfig.Location = new Point(456, 4);
            buttonNewConfig.Name = "buttonNewConfig";
            buttonNewConfig.Size = new Size(64, 23);
            buttonNewConfig.TabIndex = 21;
            buttonNewConfig.Text = "New";
            buttonNewConfig.UseVisualStyleBackColor = true;
            buttonNewConfig.Click += buttonNewConfig_Click;
            // 
            // buttonRefreshAudio
            // 
            buttonRefreshAudio.Location = new Point(456, 62);
            buttonRefreshAudio.Name = "buttonRefreshAudio";
            buttonRefreshAudio.Size = new Size(134, 23);
            buttonRefreshAudio.TabIndex = 22;
            buttonRefreshAudio.Text = "Refresh";
            buttonRefreshAudio.UseVisualStyleBackColor = true;
            buttonRefreshAudio.Click += buttonRefreshAudio_Click;
            // 
            // buttonDeleteConfig
            // 
            buttonDeleteConfig.Location = new Point(526, 4);
            buttonDeleteConfig.Name = "buttonDeleteConfig";
            buttonDeleteConfig.Size = new Size(64, 23);
            buttonDeleteConfig.TabIndex = 23;
            buttonDeleteConfig.Text = "Delete";
            buttonDeleteConfig.UseVisualStyleBackColor = true;
            buttonDeleteConfig.Click += buttonDeleteConfig_Click;
            // 
            // textBoxAppSelected
            // 
            textBoxAppSelected.Location = new Point(56, 91);
            textBoxAppSelected.Name = "textBoxAppSelected";
            textBoxAppSelected.PlaceholderText = "Select an app";
            textBoxAppSelected.ReadOnly = true;
            textBoxAppSelected.Size = new Size(392, 23);
            textBoxAppSelected.TabIndex = 24;
            // 
            // buttonAppSet
            // 
            buttonAppSet.Location = new Point(456, 91);
            buttonAppSet.Name = "buttonAppSet";
            buttonAppSet.Size = new Size(134, 23);
            buttonAppSet.TabIndex = 26;
            buttonAppSet.Text = "Set";
            buttonAppSet.UseVisualStyleBackColor = true;
            buttonAppSet.Click += buttonAppSet_Click;
            // 
            // labelInstructions
            // 
            labelInstructions.AutoSize = false;
            labelInstructions.Location = new Point(56, 200);
            labelInstructions.Name = "labelInstructions";
            labelInstructions.Size = new Size(543, 75);
            labelInstructions.TabIndex = 27;
            labelInstructions.Text = resources.GetString("labelInstructions.Text");
            labelInstructions.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // trayIcon
            // 
            trayIcon.Icon = (Icon)resources.GetObject("trayIcon.Icon");
            trayIcon.Text = "KBMixer";
            trayIcon.Visible = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(612, 285);
            MinimumSize = new Size(520, 285);
            Controls.Add(buttonResetDisplayName);
            Controls.Add(textBoxConfigDisplayName);
            Controls.Add(labelConfigDisplayName);
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
            Controls.Add(comboBoxAudioSession);
            Controls.Add(checkBoxControlSingleAppProcess);
            Controls.Add(appLabel);
            Controls.Add(deviceLabel);
            Controls.Add(deviceComboBox);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "KBMixer";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox deviceComboBox;
        private Label deviceLabel;
        private Label appLabel;
        private CheckBox checkBoxControlSingleAppProcess;
        private ComboBox comboBoxAudioSession;
        private Label labelProcessIndex;
        private Label labelConfig;
        private Label labelConfigDisplayName;
        private TextBox textBoxConfigDisplayName;
        private Button buttonResetDisplayName;
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
        private NotifyIcon trayIcon;
    }
}
