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
        public AppSelection()
        {
            InitializeComponent();
        }

        //private void PopulateAudioApps()
        //{
        //    // Update this so it only populates audio apps pertaining to the selected device / current config
        //    // Clear the combo box
        //    appComboBox.Items.Clear();
        //    // Loop through all audio apps
        //    foreach (var app in audioApps)
        //    {
        //        // Add each app to the combo box
        //        appComboBox.Items.Add(app.AppFriendlyName);
        //    }

        //    // Add the app from the current config to the combo box
        //    appComboBox.SelectedIndex = 0;
        //}

        //private void checkBoxSetAppManual_CheckedChanged(object sender, EventArgs e)
        //{
        //    textBoxAppManual.ReadOnly = !checkBoxSetAppManual.Checked;
        //    appComboBox.Enabled = !checkBoxSetAppManual.Checked;
        //}

        private void labelAppSelect_Click(object sender, EventArgs e)
        {

        }
    }
}
