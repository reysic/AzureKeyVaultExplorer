// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Secrets
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Vault.Explorer.Controls;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Dialogs.Passwords;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.Collections;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Explorer.Model.Files.Secrets;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Library;
    using ScintillaNET;
    using Settings = Microsoft.Vault.Explorer.Settings;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    public partial class SecretDialog : ItemDialogBase // <PropertyObjectSecret, SecretBundle>
    {
        private CertificateValueObject _certificateObj;
        private Scintilla uxTextBoxValue;

        private SecretDialog(ISession session, string title, ItemDialogBaseMode mode) : base(session, title, mode)
        {
            this.InitializeComponent();
            this.uxErrorProvider.SetIconAlignment(this.uxSplitContainer, ErrorIconAlignment.TopLeft);
            this.uxErrorProvider.SetIconAlignment(this.uxPropertyGridSecret, ErrorIconAlignment.TopLeft);
            this.uxErrorProvider.SetIconPadding(this.uxPropertyGridSecret, -16);

            this.SetUpTextBoxValue();
            List<string> unknownSk;
            List<SecretKind> secretKinds = LoadSecretKinds(this._session.CurrentVaultAlias, out unknownSk);

            if (unknownSk.Count > 0)
            {
                MessageBox.Show(this,
                    $"Secret kinds '{string.Join(",", unknownSk)}' in vault alias '{this._session.CurrentVaultAlias.Alias}' are being ignored because they are not found in {Settings.Default.SecretKindsJsonFileLocation}",
                    "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            this.uxMenuSecretKind.Items.AddRange(secretKinds.ToArray());
            this.uxSplitContainer_Panel1_SizeChanged(null, EventArgs.Empty);
            this.ActiveControl = this.uxTextBoxName;
        }

        /// <summary>
        ///     New empty secret
        /// </summary>
        public SecretDialog(ISession session) : this(session, "New secret", ItemDialogBaseMode.New)
        {
            this._changed = true;
            var s = new SecretBundle { Attributes = new SecretAttributes(), ContentType = ContentTypeEnumConverter.GetDescription(ContentType.Text) };
            this.RefreshSecretObject(s);
            SecretKind defaultSK = this.TryGetDefaultSecretKind();
            int defaultIndex = this.uxMenuSecretKind.Items.IndexOf(defaultSK);
            this.uxMenuSecretKind.Items[defaultIndex].PerformClick();
        }

        /// <summary>
        ///     New secret from file
        /// </summary>
        public SecretDialog(ISession session, FileInfo fi) : this(session)
        {
            var obj = (PropertyObjectSecret)this.PropertyObject;

            this.uxTextBoxName.Text = Utils.ConvertToValidSecretName(Path.GetFileNameWithoutExtension(fi.Name));
            obj.ContentType = ContentTypeUtils.FromExtension(fi.Extension);
            string password = null;
            switch (obj.ContentType)
            {
                case ContentType.Certificate:
                    break;
                case ContentType.Pkcs12:
                case ContentType.Pkcs12Base64:
                    var pwdDlg = new PasswordDialog();
                    if (pwdDlg.ShowDialog() != DialogResult.OK)
                    {
                        this.DialogResult = DialogResult.Cancel;
                        return;
                    }

                    password = pwdDlg.Password;
                    break;
                case ContentType.KeyVaultSecret:
                    var kvsf = Utils.LoadFromJsonFile<KeyVaultSecretFile>(fi.FullName);
                    SecretBundle s = kvsf.Deserialize();
                    this.uxPropertyGridSecret.SelectedObject = this.PropertyObject = new PropertyObjectSecret(s, this.SecretObject_PropertyChanged);
                    this.uxTextBoxName.Text = s.SecretIdentifier?.Name;
                    this.uxTextBoxValue.Text = s.Value;
                    return;
                default:
                    this.uxTextBoxValue.Text = File.ReadAllText(fi.FullName);
                    return;
            }

            // Certificate flow
            this.RefreshCertificate(new CertificateValueObject(fi, password));
            this.AutoDetectSecretKind();
        }

        /// <summary>
        ///     New secret from certificate
        /// </summary>
        public SecretDialog(ISession session, X509Certificate2 certificate) : this(session)
        {
            bool hasExportablePrivateKey = certificate.HasPrivateKey && (
                (certificate.GetRSAPrivateKey()?.ExportParameters(true) != null) ||
                (certificate.GetDSAPrivateKey()?.ExportParameters(true) != null));

            var obj = (PropertyObjectSecret)this.PropertyObject;
            obj.ContentType = hasExportablePrivateKey ? ContentType.Pkcs12 : ContentType.Certificate;
            this.uxTextBoxName.Text = Utils.ConvertToValidSecretName(certificate.GetNameInfo(X509NameType.SimpleName, false));
            string password = hasExportablePrivateKey ? Utils.NewSecurePassword() : null;
            byte[] data = hasExportablePrivateKey ? certificate.Export(X509ContentType.Pkcs12, password) : certificate.Export(X509ContentType.Cert);
            this.RefreshCertificate(new CertificateValueObject(Convert.ToBase64String(data), password));
            this.AutoDetectSecretKind();
        }

        /// <summary>
        ///     Edit or Copy secret
        /// </summary>
        public SecretDialog(ISession session, string name, IEnumerable<SecretItem> versions) : this(session, "Edit secret", ItemDialogBaseMode.Edit)
        {
            this.Text += $" {name}";
            int i = 0;
            this.uxMenuVersions.Items.AddRange((from v in versions orderby v.Attributes.Created descending select new SecretVersion(i++, v)).ToArray());
            this.uxMenuVersions_ItemClicked(null, new ToolStripItemClickedEventArgs(this.uxMenuVersions.Items[0])); // Pass sender as NULL so _changed will be set to false
        }

        /// <summary>Raises the <see cref="E:System.Windows.Forms.Form.Shown" /> event.</summary>
        /// <param name="e">A <see cref="T:System.EventArgs" /> that contains the event data.</param>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.uxPropertyGridSecret.SetLabelColumnWidth(250);
        }

        private static List<SecretKind> LoadSecretKinds(VaultAlias vaultAlias, out List<string> unknownSk)
        {
            SecretKinds allSecretKinds = Utils.LoadFromJsonFile<SecretKinds>(Settings.Default.SecretKindsJsonFileLocation);
            List<SecretKind> validatedSecretKinds = new List<SecretKind>(allSecretKinds.Count) ?? new List<SecretKind>(vaultAlias.SecretKinds.Length);
            unknownSk = new List<string>();

            // If there are no SecretKinds in the VaultAliases.json for a vault OR if it's a vault Not in VaultAliases, return ALL SecretKinds.
            if (vaultAlias.SecretKinds == null || (vaultAlias.SecretKinds.Length == 1 && (string)vaultAlias.SecretKinds.GetValue(0) == "Custom"))
            {
                foreach (var key in allSecretKinds.Keys)
                {
                    SecretKind sk;
                    allSecretKinds.TryGetValue(key, out sk);
                    validatedSecretKinds.Add(sk);
                }
            }
            // Otherwise, return just the specified SecretKinds
            else
            {
                foreach (var secretKind in vaultAlias.SecretKinds)
                {
                    SecretKind sk;
                    if (allSecretKinds.TryGetValue(secretKind, out sk))
                    {
                        validatedSecretKinds.Add(sk);
                    }
                    else
                    {
                        unknownSk.Add(secretKind);
                    }
                }
            }

            // Sort the Secret Kinds
            List<SecretKind> orderedValidatedSecretKinds = validatedSecretKinds.OrderBy(o => o.Alias).ToList();

            return orderedValidatedSecretKinds;
        }

        private void RefreshSecretObject(SecretBundle s)
        {
            this.PropertyObject = new PropertyObjectSecret(s, this.SecretObject_PropertyChanged);
            this.uxPropertyGridSecret.SelectedObject = this.PropertyObject;
            this.uxTextBoxName.Text = this.PropertyObject.Name;
            this.uxTextBoxValue.Text = this.PropertyObject.Value;

            // Handle Scintilla framework bug where text is not updated.
            if (this.uxTextBoxValue.Text != this.PropertyObject.Value)
            {
                // Remove and create new textbox with value
                this.uxSplitContainer.Panel1.Controls.Remove(this.uxTextBoxValue);
                this.SetUpTextBoxValue();
                this.uxTextBoxValue.Text = this.PropertyObject.Value;
            }

            var obj = (PropertyObjectSecret)this.PropertyObject;
            this.ToggleCertificateMode(obj.ContentType.IsCertificate());
            this.uxTextBoxValue.Refresh();
        }

        private void SetUpTextBoxValue()
        {
            this.uxTextBoxValue = new Scintilla();
            this.uxSplitContainer.Panel1.Controls.Add(this.uxTextBoxValue);

            // basic config
            this.uxTextBoxValue.Dock = DockStyle.Fill;
            this.uxTextBoxValue.TextChanged += this.uxTextBoxValue_TextChanged;

            //initial view config
            this.uxTextBoxValue.WrapMode = WrapMode.None;
            this.uxTextBoxValue.IndentationGuides = IndentView.LookBoth;
        }

        private void AutoDetectSecretKind()
        {
            SecretKind defaultSecretKind = this.TryGetDefaultSecretKind(); // Default is the first one which is always Custom
            SecretKind autoDetectSecretKind = new SecretKind(defaultSecretKind.Alias);
            TagItem currentSKTag = this.PropertyObject.Tags.GetOrNull(new TagItem(Consts.SecretKindKey, ""));
            bool shouldAddNew = true;

            // Read the CustomTags and determine the SecretKind
            foreach (SecretKind sk in this.uxMenuSecretKind.Items) // Auto detect 'last' secret kind based on the name only
            {
                if (currentSKTag == null)
                {
                    autoDetectSecretKind = defaultSecretKind;
                    shouldAddNew = false;
                    break;
                }

                // If the current Secret Kind is in the list of menu items,
                if (currentSKTag.Value == sk.Alias)
                {
                    autoDetectSecretKind = sk;
                    shouldAddNew = false;
                    break;
                }
            }

            if (shouldAddNew)
            {
                autoDetectSecretKind = new SecretKind(currentSKTag.Value);
                this.uxMenuSecretKind.Items.Add(autoDetectSecretKind);
            }

            // Apply last found secret kind, only when both Content Type and SecretKind are certificate or both not, otherwise fallback to Custom (the first one)
            var obj = (PropertyObjectSecret)this.PropertyObject;
            if ((!obj.ContentType.IsCertificate() || !autoDetectSecretKind.IsCertificate) &&
                (obj.ContentType.IsCertificate() || autoDetectSecretKind.IsCertificate))
            {
                autoDetectSecretKind = this.TryGetDefaultSecretKind();
            }

            this._certificateObj = obj.ContentType.IsCertificate() ? CertificateValueObject.FromValue(this.uxTextBoxValue.Text) : null;
            autoDetectSecretKind?.PerformClick();
        }

        private SecretKind TryGetDefaultSecretKind(string alias = "Custom")
        {
            foreach (SecretKind sk in this.uxMenuSecretKind.Items)
            {
                if (sk.Alias == alias)
                {
                    return sk;
                }
            }

            return (SecretKind)this.uxMenuSecretKind.Items[0];
        }

        private void ToggleCertificateMode(bool enable)
        {
            this.uxTextBoxValue.ReadOnly = enable;
            this.uxLinkLabelViewCertificate.Visible = enable;
        }

        private void RefreshCertificate(CertificateValueObject cvo)
        {
            this._certificateObj = cvo;
            if (this._certificateObj != null)
            {
                this.uxTextBoxValue.ReadOnly = false;
                this._certificateObj.FillTagsAndExpiration(this.PropertyObject);
                this.uxTextBoxValue.Text = this._certificateObj.ToValue(this.PropertyObject.SecretKind.CertificateFormat);
                this.uxTextBoxValue.Refresh();
            }

            this.ToggleCertificateMode(this._certificateObj != null);
        }

        private void uxTextBoxValue_TextChanged(object sender, EventArgs e)
        {
            this._changed = true;
            this.PropertyObject.Value = this.uxTextBoxValue.Text;
            this.uxTimerValueTypingCompleted.Stop(); // Wait for user to finish the typing in a text box
            this.uxTimerValueTypingCompleted.Start();
        }

        private void SecretObject_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this._changed = true;
            var obj = (PropertyObjectSecret)this.PropertyObject;
            if (e.PropertyName == nameof(obj.ContentType)) // ContentType changed, refresh
            {
                this.AutoDetectSecretKind();
                this.RefreshCertificate(this._certificateObj);
                this.uxTextBoxValue_TextChanged(sender, null);
            }

            string tagsExpirationError = this.PropertyObject.AreCustomTagsValid();
            if (false == this.PropertyObject.IsExpirationValid)
            {
                tagsExpirationError += $"Expiration values are invalid: 'Valid from time' must be less then 'Valid until time' and expiration period must be less or equal to {Utils.ExpirationToString(this.PropertyObject.SecretKind.MaxExpiration)}";
            }

            this.uxErrorProvider.SetError(this.uxPropertyGridSecret, string.IsNullOrEmpty(tagsExpirationError) ? null : tagsExpirationError);

            this.InvalidateOkButton();
        }

        private void uxTimerValueTypingCompleted_Tick(object sender, EventArgs e)
        {
            this.uxTimerValueTypingCompleted.Stop();
            bool valueValid = this.PropertyObject.IsValueValid;
            this.uxErrorProvider.SetError(this.uxSplitContainer, valueValid ? null : $"Secret value must match the following regex:\n{this.PropertyObject.SecretKind.ValueRegex}");

            int rawValueLength = this.PropertyObject.RawValue.Length;
            this.uxLabelBytesLeft.Text = $"{rawValueLength:N0} bytes / {Consts.MaxSecretValueLength - rawValueLength:N0} bytes left";
            if (valueValid) // Make sure that we are in the 25KB limit
            {
                valueValid = rawValueLength >= 1 && rawValueLength <= Consts.MaxSecretValueLength;
                this.uxErrorProvider.SetError(this.uxSplitContainer, valueValid ? null : $"Secret value length must be in the following range [1..{Consts.MaxSecretValueLength}]");
            }

            this.InvalidateOkButton();
        }

        protected override void uxMenuSecretKind_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var sk = (SecretKind)e.ClickedItem;
            if (sk.Checked)
            {
                return; // Same item was clicked
            }

            foreach (var item in this.uxMenuSecretKind.Items)
            {
                ((SecretKind)item).Checked = false;
            }

            this.PropertyObject.AddOrUpdateSecretKind(sk);
            this.PropertyObject.SecretKind = sk;
            this.PropertyObject.PopulateCustomTags();
            // Populate default expiration and value template in case this is a new secret
            if (this._mode == ItemDialogBaseMode.New)
            {
                this.PropertyObject.PopulateExpiration();
                this.uxTextBoxValue.Text = sk.ValueTemplate;
            }

            sk.Checked = true;
            this.uxLinkLabelSecretKind.Text = sk.ToString();
            this.uxToolTip.SetToolTip(this.uxLinkLabelSecretKind, sk.Description);
            this.RefreshCertificate(this._certificateObj);
            this.uxTextBoxName_TextChanged(sender, null);
            this.uxTextBoxValue_TextChanged(sender, null);
            this.uxPropertyGridSecret.Refresh();
        }

        protected override async Task<object> OnVersionChangeAsync(CustomVersion cv)
        {
            SecretVersion sv = (SecretVersion)cv;
            var s = await this._session.CurrentVault.GetSecretAsync(sv.SecretItem.Identifier.Name, sv.SecretItem.Identifier.Version);
            this.RefreshSecretObject(s);
            this.AutoDetectSecretKind();
            return s;
        }

        protected override ContextMenuStrip GetNewValueMenu() => this.uxMenuNewValue;

        private void uxMenuItemNewPassword_Click(object sender, EventArgs e)
        {
            if (this.uxTextBoxValue.ReadOnly)
            {
                return;
            }

            this.uxTextBoxValue.Text = Utils.NewSecurePassword();
            this.uxTextBoxValue.Refresh();
        }

        private void uxMenuItemNewGuid_Click(object sender, EventArgs e)
        {
            if (this.uxTextBoxValue.ReadOnly)
            {
                return;
            }

            this.uxTextBoxValue.Text = Guid.NewGuid().ToString("D");
            this.uxTextBoxValue.Refresh();
        }

        private void uxMenuItemNewApiKey_Click(object sender, EventArgs e)
        {
            if (this.uxTextBoxValue.ReadOnly)
            {
                return;
            }

            this.uxTextBoxValue.Text = Utils.NewApiKey();
            this.uxTextBoxValue.Refresh();
        }

        private void uxSplitContainer_Panel1_SizeChanged(object sender, EventArgs e)
        {
            this.uxLinkLabelViewCertificate.Left = (this.uxSplitContainer.Panel1.Width - this.uxLinkLabelViewCertificate.Width) / 2;
            this.uxLinkLabelViewCertificate.Top = (this.uxSplitContainer.Panel1.Height - this.uxLinkLabelViewCertificate.Height) / 2;
        }

        private void uxLinkLabelViewCertificate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            X509Certificate2UI.DisplayCertificate(this._certificateObj.Certificate, this.Handle);
        }
    }
}