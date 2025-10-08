// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Passwords
{
    using System;
    using System.Windows.Forms;

    public partial class PasswordDialog : Form
    {
        public PasswordDialog()
        {
            this.InitializeComponent();
        }

        public string Password => this.uxTextBoxPassword.Text;

        private void uxCheckBoxDisplayPwd_CheckedChanged(object sender, EventArgs e)
        {
            this.uxTextBoxPassword.UseSystemPasswordChar = !this.uxCheckBoxDisplayPwd.Checked;
        }
    }
}