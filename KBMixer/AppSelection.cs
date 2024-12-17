using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KBMixer
{
    public partial class AppSelection : Form
    {
        // Default to selecting an active app, false means get value for manual entry
        private bool isSelect = true;
        public string? SelectedAppFriendlyName { get; private set; }

        public AppSelection(AudioApp[] audioApps, string appFileName)
        {
            InitializeComponent();
            SetAppSelection(appFileName);
            PopulateActiveAppsSelection(audioApps);
        }

        // Disable ComboBox when Manual Radio Button is Selected
        private void radioEnter_CheckedChanged(object sender, EventArgs e)
        {
            if (radioEnter.Checked)
            {
                comboBoxSelect.Enabled = false;
                textBoxEnter.Enabled = true;
                textBoxEnter.ReadOnly = false;
                isSelect = false;
            }
        }

        // Make TextBox Read Only when Drop-Down Radio Button is Selected
        private void radioSelect_CheckedChanged(object sender, EventArgs e)
        {
            if (radioSelect.Checked)
            {
                comboBoxSelect.Enabled = true;
                textBoxEnter.Enabled = false;
                textBoxEnter.ReadOnly = true;
                isSelect = true;
            }
        }

        // Populate Active Apps in ComboBox
        private void PopulateActiveAppsSelection(AudioApp[] audioApps)
        {
            comboBoxSelect.Items.Clear();
            var uniqueAppNames = new HashSet<string>();
            foreach (var app in audioApps)
            {
                if (uniqueAppNames.Add(app.AppFriendlyName))
                {
                    comboBoxSelect.Items.Add(app.AppFriendlyName);
                }
            }
        }

        // Pre-populate Dialog Based on Existing App Selection
        public void SetAppSelection(string appFileName)
        {
            if (comboBoxSelect.Items.Contains(appFileName))
            {
                comboBoxSelect.SelectedItem = appFileName;
            }
            else
            {
                textBoxEnter.Text = appFileName;
            }
        }

        // Clicking OK Button should Update Config and Save Configuration to Disk
        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (isSelect)
            {
                if (comboBoxSelect.SelectedItem == null)
                {
                    MessageBox.Show("Please select an application from the list.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SelectedAppFriendlyName = comboBoxSelect.SelectedItem.ToString();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(textBoxEnter.Text))
                {
                    MessageBox.Show("Please enter a valid application file name (e.g., chrome.exe).", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SelectedAppFriendlyName = textBoxEnter.Text;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // Clicking Cancel Button should Close the Form without updating Config
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
