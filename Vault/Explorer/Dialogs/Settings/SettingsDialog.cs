// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Settings
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;
    using Microsoft.Vault.Library;
    using Settings = Microsoft.Vault.Explorer.Settings;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    public partial class SettingsDialog : Form
    {
        private readonly Settings _currentSettings;

        public SettingsDialog()
        {
            this.InitializeComponent();
            this.uxPropertyGrid.SelectedObject = this._currentSettings = new Settings();
            this._currentSettings.PropertyChanged += (sender, e) => { this.uxButtonOK.Enabled = true; };
            this.uxTextBoxVersions.Text = this.FetchVersions();

            string licenseTxt = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "License.txt");
            if (File.Exists(licenseTxt))
            {
                this.uxTextBoxLicense.Text = File.ReadAllText(licenseTxt).Replace("\n", "\r\n");
            }
        }

        private void uxButtonOK_Click(object sender, EventArgs e)
        {
            this._currentSettings.Save();
            Settings.Default.Reload();
        }

        private void uxLinkLabelTitle_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo { FileName = Globals.GitHubUrl, UseShellExecute = true };
            Process.Start(sInfo);
        }

        private void uxLinkLabelSendFeedback_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo { FileName = Globals.GitHubIssuesUrl, UseShellExecute = true };
            Process.Start(sInfo);
        }

        private void uxLinkLabelInstallLocation_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo { FileName = Path.GetDirectoryName(Application.ExecutablePath), UseShellExecute = true };
            Process.Start(sInfo);
        }

        private void uxLinkLabelUserSettingsLocation_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            var configFolder = Path.GetDirectoryName(config.FilePath);

            // Ensure the user settings file exists before trying to open the folder.
            if (!File.Exists(config.FilePath))
            {
                this._currentSettings.Save();
            }

            ProcessStartInfo sInfo = new ProcessStartInfo { FileName = configFolder, UseShellExecute = true };
            Process.Start(sInfo);
        }

        private string FetchVersions()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Utils.GetFileVersionString(string.Format("{0} version: ", Globals.AppName), Path.GetFileName(Application.ExecutablePath), string.Format(" ({0})", Environment.Is64BitProcess ? "x64" : "x86")));
            sb.AppendLine(string.Format(".NET framework version: {0}", Environment.Version));
            sb.AppendLine(Utils.GetFileVersionString("Microsoft.Azure.KeyVault.dll version: ", "Microsoft.Azure.KeyVault.dll"));
            sb.AppendLine(Utils.GetFileVersionString("Microsoft.Azure.Management.KeyVault.dll version: ", "Microsoft.Azure.Management.KeyVault.dll"));
            return sb.ToString();
        }

        private void uxLinkLabelClearTokenCache_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FileTokenCache.ClearAllFileTokenCaches();
        }
    }
}