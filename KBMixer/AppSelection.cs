using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        private string? SelectedAppFileName;
        public AppSelection(AudioApp[] audioApps)
        {
            InitializeComponent();
            AppSelection_Load(audioApps);
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
        private void AppSelection_Load(AudioApp[] audioApps)
        {
            comboBoxSelect.Items.Clear();
            foreach (var app in audioApps)
            {
                comboBoxSelect.Items.Add(app.AppFriendlyName);
            }
        }

        // Clicking OK Button should Update Config and Save Configuration to Disk

        // Clicking Cancel Button should Close the Form without updating Config
    }
}
