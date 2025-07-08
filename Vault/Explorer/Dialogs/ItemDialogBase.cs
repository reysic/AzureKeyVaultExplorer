// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs
{
    using System;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.PropObjects;

    public partial class ItemDialogBase : Form
    {
        protected readonly ISession _session;
        protected readonly ItemDialogBaseMode _mode;
        protected bool _changed;
        public object OriginalObject; //  Will be NULL in New mode and current value in case of Edit mode
        public PropertyObject PropertyObject { get; protected set; }

        public ItemDialogBase()
        {
        }

        public ItemDialogBase(ISession session, string title, ItemDialogBaseMode mode)
        {
            this.InitializeComponent();
            this._session = session;
            this.Text = title;
            this._mode = mode;
        }

        protected virtual void InvalidateOkButton()
        {
            string tagsError = this.PropertyObject.AreCustomTagsValid();
            this.uxButtonOK.Enabled = this._changed && this.PropertyObject.IsNameValid && this.PropertyObject.IsValueValid &&
                                      this.PropertyObject.IsExpirationValid && string.IsNullOrEmpty(tagsError);
        }

        protected virtual void uxTextBoxName_TextChanged(object sender, EventArgs e)
        {
            this.PropertyObject.Name = this.uxTextBoxName.Text;
            this._changed = true;
            this.uxErrorProvider.SetError(this.uxTextBoxName, this.PropertyObject.IsNameValid ? null : $"Name must match the following regex:\n{this.PropertyObject.SecretKind.NameRegex}");
            this.InvalidateOkButton();
        }

        protected virtual void uxLinkLabelSecretKind_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.uxMenuSecretKind.Show(this.uxLinkLabelSecretKind, 0, this.uxLinkLabelSecretKind.Height);
        }

        protected virtual void uxMenuSecretKind_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
        }

        protected virtual Task<object> OnVersionChangeAsync(CustomVersion cv) => null;

        protected async void uxMenuVersions_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var cv = (CustomVersion)e.ClickedItem;
            if (cv.Checked)
            {
                return; // Same item was clicked
            }

            foreach (var item in this.uxMenuVersions.Items)
            {
                ((CustomVersion)item).Checked = false;
            }

            var u = await this.OnVersionChangeAsync(cv);
            this.OriginalObject = null == this.OriginalObject ? u : this.OriginalObject;

            cv.Checked = true;
            this.uxLinkLabelValue.Text = cv.ToString();
            this.uxToolTip.SetToolTip(this.uxLinkLabelValue, cv.ToolTipText);
            this._changed = sender != null; // Sender will be NULL for the first time during Edit mode ctor
            this.InvalidateOkButton();
        }

        protected virtual ContextMenuStrip GetNewValueMenu() => null;

        private void uxLinkLabelValue_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            switch (this._mode)
            {
                case ItemDialogBaseMode.New:
                    this.GetNewValueMenu()?.Show(this.uxLinkLabelValue, 0, this.uxLinkLabelValue.Height);
                    return;
                case ItemDialogBaseMode.Edit:
                    this.uxMenuVersions.Show(this.uxLinkLabelValue, 0, this.uxLinkLabelValue.Height);
                    return;
            }
        }
    }
}