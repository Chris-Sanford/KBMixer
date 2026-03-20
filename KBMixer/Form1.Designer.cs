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
            checkBoxDeviceMasterVolume = new CheckBox();
            appLabel = new Label();
            checkBoxControlSingleAppProcess = new CheckBox();
            comboBoxAudioSession = new ComboBox();
            labelSession = new Label();
            labelConfig = new Label();
            labelConfigDisplayName = new Label();
            textBoxConfigDisplayName = new TextBox();
            buttonResetDisplayName = new Button();
            comboBoxConfig = new ComboBox();
            labelHotkeys = new Label();
            textboxHotkeys = new TextBox();
            buttonHotkeyAdd = new Button();
            buttonHotkeyReset = new Button();
            buttonNewConfig = new Button();
            buttonRefreshAudio = new Button();
            buttonDeleteConfig = new Button();
            textBoxAppSelected = new TextBox();
            buttonAppSet = new Button();
            groupBoxHowTo = new GroupBox();
            labelInstructions = new Label();
            checkBoxOpenAtStartup = new CheckBox();
            trayIcon = new NotifyIcon(components);
            groupBoxHowTo.SuspendLayout();
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
            // checkBoxDeviceMasterVolume
            // 
            checkBoxDeviceMasterVolume.AccessibleDescription = "When checked, hotkeys and mouse wheel adjust the entire output device volume instead of one app";
            checkBoxDeviceMasterVolume.AutoSize = true;
            checkBoxDeviceMasterVolume.Location = new Point(56, 88);
            checkBoxDeviceMasterVolume.Name = "checkBoxDeviceMasterVolume";
            checkBoxDeviceMasterVolume.Size = new Size(318, 19);
            checkBoxDeviceMasterVolume.TabIndex = 31;
            checkBoxDeviceMasterVolume.Text = "Control output device master volume (all apps on this device)";
            checkBoxDeviceMasterVolume.UseVisualStyleBackColor = true;
            checkBoxDeviceMasterVolume.CheckedChanged += checkBoxDeviceMasterVolume_CheckedChanged;
            // 
            // appLabel
            // 
            appLabel.AutoSize = true;
            appLabel.Location = new Point(7, 149);
            appLabel.Name = "appLabel";
            appLabel.Size = new Size(29, 15);
            appLabel.TabIndex = 3;
            appLabel.Text = "App";
            // 
            // checkBoxControlSingleAppProcess
            // 
            checkBoxControlSingleAppProcess.AccessibleDescription = "When checked, volume changes apply only to the audio session selected in the list";
            checkBoxControlSingleAppProcess.AutoSize = true;
            checkBoxControlSingleAppProcess.Location = new Point(56, 175);
            checkBoxControlSingleAppProcess.Name = "checkBoxControlSingleAppProcess";
            checkBoxControlSingleAppProcess.Size = new Size(165, 19);
            checkBoxControlSingleAppProcess.TabIndex = 8;
            checkBoxControlSingleAppProcess.Text = "Control single app process";
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
            comboBoxAudioSession.Location = new Point(280, 173);
            comboBoxAudioSession.Name = "comboBoxAudioSession";
            comboBoxAudioSession.Size = new Size(220, 23);
            comboBoxAudioSession.TabIndex = 9;
            comboBoxAudioSession.SelectedIndexChanged += comboBoxAudioSession_SelectedIndexChanged;
            // 
            // labelSession
            // 
            labelSession.AutoSize = true;
            labelSession.Location = new Point(7, 178);
            labelSession.Name = "labelSession";
            labelSession.Size = new Size(48, 15);
            labelSession.TabIndex = 10;
            labelSession.Text = "Session";
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
            labelHotkeys.Location = new Point(7, 118);
            labelHotkeys.Name = "labelHotkeys";
            labelHotkeys.Size = new Size(50, 15);
            labelHotkeys.TabIndex = 13;
            labelHotkeys.Text = "Hotkeys";
            // 
            // textboxHotkeys
            // 
            textboxHotkeys.ImeMode = ImeMode.On;
            textboxHotkeys.Location = new Point(56, 115);
            textboxHotkeys.Name = "textboxHotkeys";
            textboxHotkeys.PlaceholderText = "Add hotkeys";
            textboxHotkeys.ReadOnly = true;
            textboxHotkeys.Size = new Size(392, 23);
            textboxHotkeys.TabIndex = 14;
            // 
            // buttonHotkeyAdd
            // 
            buttonHotkeyAdd.Location = new Point(456, 115);
            buttonHotkeyAdd.Name = "buttonHotkeyAdd";
            buttonHotkeyAdd.Size = new Size(64, 23);
            buttonHotkeyAdd.TabIndex = 15;
            buttonHotkeyAdd.Text = "Add";
            buttonHotkeyAdd.UseVisualStyleBackColor = true;
            buttonHotkeyAdd.Click += buttonHotkeyAdd_Click;
            // 
            // buttonHotkeyReset
            // 
            buttonHotkeyReset.Location = new Point(526, 115);
            buttonHotkeyReset.Name = "buttonHotkeyReset";
            buttonHotkeyReset.Size = new Size(64, 23);
            buttonHotkeyReset.TabIndex = 16;
            buttonHotkeyReset.Text = "Reset";
            buttonHotkeyReset.UseVisualStyleBackColor = true;
            buttonHotkeyReset.Click += buttonHotkeyReset_Click;
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
            textBoxAppSelected.Location = new Point(56, 146);
            textBoxAppSelected.Name = "textBoxAppSelected";
            textBoxAppSelected.PlaceholderText = "Select an app";
            textBoxAppSelected.ReadOnly = true;
            textBoxAppSelected.Size = new Size(392, 23);
            textBoxAppSelected.TabIndex = 24;
            // 
            // buttonAppSet
            // 
            buttonAppSet.Location = new Point(456, 146);
            buttonAppSet.Name = "buttonAppSet";
            buttonAppSet.Size = new Size(134, 23);
            buttonAppSet.TabIndex = 26;
            buttonAppSet.Text = "Set";
            buttonAppSet.UseVisualStyleBackColor = true;
            buttonAppSet.Click += buttonAppSet_Click;
            // 
            // groupBoxHowTo
            // 
            groupBoxHowTo.Location = new Point(8, 212);
            groupBoxHowTo.Name = "groupBoxHowTo";
            groupBoxHowTo.Size = new Size(596, 120);
            groupBoxHowTo.TabIndex = 32;
            groupBoxHowTo.TabStop = false;
            groupBoxHowTo.Text = "How to use";
            // 
            // labelInstructions
            // 
            labelInstructions.AccessibleDescription = "Instructions for using KBMixer";
            labelInstructions.AccessibleName = "How to use";
            labelInstructions.AutoSize = false;
            labelInstructions.Location = new Point(12, 24);
            labelInstructions.Name = "labelInstructions";
            labelInstructions.Size = new Size(560, 88);
            labelInstructions.TabIndex = 0;
            labelInstructions.Text = resources.GetString("labelInstructions.Text");
            labelInstructions.TextAlign = ContentAlignment.TopCenter;
            groupBoxHowTo.Controls.Add(labelInstructions);
            // 
            // checkBoxOpenAtStartup
            // 
            checkBoxOpenAtStartup.AccessibleDescription = "Run KBMixer when you sign in to Windows; starts minimized to the notification area";
            checkBoxOpenAtStartup.AccessibleName = "Open at Windows startup";
            checkBoxOpenAtStartup.AutoSize = true;
            checkBoxOpenAtStartup.Location = new Point(56, 340);
            checkBoxOpenAtStartup.Name = "checkBoxOpenAtStartup";
            checkBoxOpenAtStartup.Size = new Size(151, 19);
            checkBoxOpenAtStartup.TabIndex = 33;
            checkBoxOpenAtStartup.Text = "Open at Windows startup";
            checkBoxOpenAtStartup.UseVisualStyleBackColor = true;
            checkBoxOpenAtStartup.CheckedChanged += checkBoxOpenAtStartup_CheckedChanged;
            // 
            // trayIcon
            // 
            trayIcon.Icon = null;
            trayIcon.Text = "KBMixer";
            trayIcon.Visible = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(612, 416);
            MinimumSize = new Size(520, 360);
            Controls.Add(checkBoxOpenAtStartup);
            Controls.Add(checkBoxDeviceMasterVolume);
            Controls.Add(buttonResetDisplayName);
            Controls.Add(textBoxConfigDisplayName);
            Controls.Add(labelConfigDisplayName);
            Controls.Add(groupBoxHowTo);
            Controls.Add(buttonAppSet);
            Controls.Add(textBoxAppSelected);
            Controls.Add(buttonDeleteConfig);
            Controls.Add(buttonRefreshAudio);
            Controls.Add(buttonNewConfig);
            Controls.Add(labelSession);
            Controls.Add(buttonHotkeyReset);
            Controls.Add(buttonHotkeyAdd);
            Controls.Add(textboxHotkeys);
            Controls.Add(labelHotkeys);
            Controls.Add(labelConfig);
            Controls.Add(comboBoxConfig);
            Controls.Add(comboBoxAudioSession);
            Controls.Add(checkBoxControlSingleAppProcess);
            Controls.Add(appLabel);
            Controls.Add(deviceLabel);
            Controls.Add(deviceComboBox);
            Icon = null;
            Name = "Form1";
            Text = "KBMixer";
            groupBoxHowTo.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox deviceComboBox;
        private Label deviceLabel;
        private CheckBox checkBoxDeviceMasterVolume;
        private Label appLabel;
        private CheckBox checkBoxControlSingleAppProcess;
        private ComboBox comboBoxAudioSession;
        private Label labelSession;
        private Label labelConfig;
        private Label labelConfigDisplayName;
        private TextBox textBoxConfigDisplayName;
        private Button buttonResetDisplayName;
        private ComboBox comboBoxConfig;
        private Label labelHotkeys;
        private TextBox textboxHotkeys;
        private Button buttonHotkeyAdd;
        private Button buttonHotkeyReset;
        private Button buttonNewConfig;
        private Button buttonRefreshAudio;
        private Button buttonDeleteConfig;
        private TextBox textBoxAppSelected;
        private Button buttonAppSet;
        private GroupBox groupBoxHowTo;
        private Label labelInstructions;
        private CheckBox checkBoxOpenAtStartup;
        private NotifyIcon trayIcon;
    }
}
