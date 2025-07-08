// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Exceptions
{
    using System;
    using System.Windows.Forms;
    using Microsoft.Vault.Explorer.Common;

    public partial class ExceptionDialog : Form
    {
        public ExceptionDialog(Exception e)
        {
            this.InitializeComponent();
            this.uxRichTextBoxCaption.Rtf = string.Format(@"{{\rtf1\ansi Oops... Unhandled exception of type \b {0} \b0 has occurred: \b {1} \b0 To ignore this error just click Continue, otherwise click Quit.}}", e.GetType(), Utils.GetRtfUnicodeEscapedString(e.Message));
            this.uxTextBoxExceptionDetails.Text = e.ToString();
            this.uxTextBoxExceptionDetails.Select(0, 0);
        }

        private void uxButtonQuit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}