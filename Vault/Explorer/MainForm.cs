// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Vault.Core;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Config;
    using Microsoft.Vault.Explorer.Controls;
    using Microsoft.Vault.Explorer.Controls.Lists;
    using Microsoft.Vault.Explorer.Dialogs.Certificates;
    using Microsoft.Vault.Explorer.Dialogs.Secrets;
    using Microsoft.Vault.Explorer.Dialogs.Settings;
    using Microsoft.Vault.Explorer.Dialogs.Subscriptions;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Explorer.Properties;
    using Microsoft.Vault.Library;
    using Action = System.Action;
    using UISettings = Microsoft.Vault.Explorer.Properties.Settings;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    public partial class MainForm : Form, ISession
    {
        private readonly ActivationUri _activationUri;
        private readonly Cursor _moveSecretCursor;
        private readonly Cursor _moveValueCursor;
        private readonly Cursor _moveLinkCursor;
        private bool _keyDownOccured;
        private readonly ToolStripButton uxButtonCancel;
        private readonly Dictionary<string, VaultAlias> _tempVaultAliases; // Temporary picked VaultAliases via SubscriptionsManager
        private const string AddNewVaultText = "How to add new vault here...";
        private const string PickVaultText = "Pick vault from subscription...";

        #region ISession

        public VaultAlias CurrentVaultAlias { get; private set; }

        public Vault CurrentVault { get; private set; }

        public ListViewSecrets ListViewSecrets => this.uxListViewSecrets;

        #endregion

        public MainForm()
        {
            this.InitializeComponent();
            this.ApplySettings();

            ToolStripManager.RenderMode = ToolStripManagerRenderMode.System;

            this._moveSecretCursor = Utils.LoadCursorFromResource(Resources.move_secret);
            this._moveValueCursor = Utils.LoadCursorFromResource(Resources.move_value);
            this._moveLinkCursor = Utils.LoadCursorFromResource(Resources.move_link);

            this.uxButtonCancel = new ToolStripButton("", Resources.cancel)
            {
                Margin = new Padding(0, 0, 20, 0),
                Size = new Size(this.uxStatusProgressBar.Width, this.uxStatusProgressBar.Width),
                ToolTipText = "Cancel operation",
                Visible = false,
            };
            this.uxStatusStrip.Items.Insert(3, this.uxButtonCancel);
            this._tempVaultAliases = new Dictionary<string, VaultAlias>();
        }

        public MainForm(ActivationUri activationUri) : this()
        {
            try
            {
                Guard.ArgumentNotNull(activationUri, nameof(activationUri));
                this._activationUri = activationUri;
                // ActivaionUri is Empty, nothing special to do
                if (this._activationUri == ActivationUri.Empty)
                {
                    return;
                }

                // Activation by vault://name
                this.uxComboBoxVaultAlias_DropDown(this, EventArgs.Empty);
                this.uxComboBoxVaultAlias.SelectedIndex = 0;
                this.SetCurrentVaultAlias();
                if (!string.IsNullOrEmpty(this._activationUri.VaultName) && string.IsNullOrEmpty(this._activationUri.ItemName))
                {
                    this.uxMenuItemRefresh.PerformClick(); // Refresh list
                    return;
                }

                // Activation by vault://name/collection/itemName
                this.SetCurrentVault();
                this._activationUri.PerformAction(this.CurrentVault);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}",
                    "Error during Activation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Raises the <see cref="E:System.Windows.Forms.Form.Shown" /> event.</summary>
        /// <param name="e">A <see cref="T:System.EventArgs" /> that contains the event data.</param>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.uxPropertyGridSecret.SetLabelColumnWidth(250);
        }

        private void ApplySettings()
        {
            this.Size = UISettings.Default.MainFormWindowSize;
            this.uxListViewSecrets.Sorting = UISettings.Default.MainFormSecretsSorting;
            this.uxListViewSecrets.SortingColumn = UISettings.Default.MainFormSecretsSortingColumn;
            this.uxButtonCopy.ToolTipText = this.uxMenuItemCopy.ToolTipText = $"Copy secret value to clipboard for {Settings.Default.CopyToClipboardTimeToLive.TotalSeconds} seconds";
        }

        private void SaveSettings()
        {
            UISettings.Default.MainFormLocation = this.WindowState == FormWindowState.Normal ? this.Location : this.RestoreBounds.Location;
            UISettings.Default.MainFormWindowSize = this.WindowState == FormWindowState.Normal ? this.Size : this.RestoreBounds.Size;
            UISettings.Default.MainFormSecretsSorting = this.uxListViewSecrets.Sorting;
            UISettings.Default.MainFormSecretsSortingColumn = this.uxListViewSecrets.SortingColumn;
            UISettings.Default.Save();
            Settings.Default.Save();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Check if there are any new vaults that haven't been saved
            var newVaults = new List<VaultAlias>();
            var processedAliases = new HashSet<string>();

            // Check temporary vaults
            foreach (var vault in this._tempVaultAliases.Values)
            {
                if (vault.IsNew && !processedAliases.Contains(vault.Alias))
                {
                    newVaults.Add(vault);
                    processedAliases.Add(vault.Alias);
                }
            }

            // Check vaults in the dropdown (avoid duplicates)
            foreach (var item in this.uxComboBoxVaultAlias.Items)
            {
                if (item is VaultAlias vault && vault.IsNew && !processedAliases.Contains(vault.Alias))
                {
                    newVaults.Add(vault);
                    processedAliases.Add(vault.Alias);
                }
            }

            // If there are new vaults, show save confirmation
            if (newVaults.Count > 0)
            {
                var result = MessageBox.Show(
                    $"You have added {newVaults.Count} new vault(s) to the list. Would you like to save them to your configuration?",
                    "Save Vault List",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);

                switch (result)
                {
                    case DialogResult.Yes:
                        // Save all new vaults
                        if (this.SaveNewVaults(newVaults))
                        {
                            ////MessageBox.Show($"Successfully saved {newVaults.Count} vault(s) to your configuration.",
                            ////    "Vault Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            // Just exit
                        }
                        else
                        {
                            // If save failed, cancel the close operation
                            e.Cancel = true;
                            return;
                        }

                        break;

                    case DialogResult.No:
                        // Don't save, just close
                        break;

                    case DialogResult.Cancel:
                        // Cancel the close operation
                        e.Cancel = true;
                        return;
                }
            }

            this.SaveSettings();
        }

        private bool SaveNewVaults(List<VaultAlias> newVaults)
        {
            try
            {
                foreach (var vault in newVaults)
                {
                    bool success = VaultConfigurationManager.AddVaultConfiguration(
                        vault.VaultNames[0], // vault name
                        vault.Alias, // alias name
                        0, // Interactive authentication (default)
                        vault.DomainHint,
                        vault.UserAlias
                    );

                    if (!success)
                    {
                        MessageBox.Show($"Failed to save vault '{vault.VaultNames[0]}'. Please check the application logs for details.",
                            "Vault Configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving vault configuration: {ex.Message}",
                    "Vault Configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private UxOperation NewUxOperationWithProgress(params ToolStripItem[] controlsToToggle) => new UxOperation(this.CurrentVaultAlias, this.uxStatusLabel, this.uxStatusProgressBar, this.uxButtonCancel, controlsToToggle);

        private UxOperation NewUxOperation(params ToolStripItem[] controlsToToggle) => new UxOperation(this.CurrentVaultAlias, this.uxStatusLabel, null, null, controlsToToggle);

        private void uxComboBoxVaultAlias_DropDown(object sender, EventArgs e)
        {
            object prevSelectedItem = this.uxComboBoxVaultAlias.SelectedItem;
            this.uxComboBoxVaultAlias.Items.Clear();
            List<VaultAlias> va = Utils.LoadFromJsonFile<VaultAliases>(Settings.Default.VaultAliasesJsonFileLocation);
            if (!string.IsNullOrEmpty(this._activationUri.VaultName)) // In case vault name was provided during activation, search for it, if not found let us add it on the fly
            {
                va = (from v in va where v.VaultNames.Contains(this._activationUri.VaultName, StringComparer.CurrentCultureIgnoreCase) select v).ToList();
                if (0 == va.Count) // Not found, let add new vault alias == vault name, with Custom secret kind
                {
                    va = Enumerable.Repeat(new VaultAlias(this._activationUri.VaultName, new[] { this._activationUri.VaultName }, new[] { "Custom" }), 1).ToList();
                }
            }

            // Mark all vaults loaded from JSON as not new
            foreach (var vault in va)
            {
                vault.IsNew = false;
            }

            this.uxComboBoxVaultAlias.Items.AddRange(va.ToArray());
            this.uxComboBoxVaultAlias.Items.AddRange(this._tempVaultAliases.Values.ToArray());
            this.uxComboBoxVaultAlias.Items.Add(AddNewVaultText);
            this.uxComboBoxVaultAlias.Items.Add(PickVaultText);
            this.uxComboBoxVaultAlias.SelectedItem = prevSelectedItem;
        }

        private void uxComboBoxVaultAlias_DropDownClosed(object sender, EventArgs e)
        {
            if (this.SetCurrentVaultAlias())
            {
                this.uxMenuItemRefresh.PerformClick();
            }
        }

        private void RefreshItemsCount()
        {
            this.uxStatusLabelSecertsCount.Text = string.IsNullOrWhiteSpace(this.uxTextBoxSearch.Text) ? $"{this.uxListViewSecrets.Items.Count} items" : $"{this.uxListViewSecrets.SearchResultsCount} out of {this.uxListViewSecrets.Items.Count} items";
            this.uxStatusLabelSecretsSelected.Text = $"{this.uxListViewSecrets.SelectedItems.Count} selected";
        }

        private bool SetCurrentVaultAlias()
        {
            if (null == this.uxComboBoxVaultAlias.SelectedItem)
            {
                return false;
            }

            // Ignore selection of the same vault alias, only when list view is not empty
            if (this.CurrentVaultAlias?.Alias == this.uxComboBoxVaultAlias.SelectedItem.ToString() && this.uxListViewSecrets.Items.Count > 0)
            {
                return false;
            }

            if (this.uxComboBoxVaultAlias.SelectedItem is string)
            {
                switch (this.uxComboBoxVaultAlias.SelectedItem.ToString())
                {
                    case AddNewVaultText:
                        this.uxButtonHelp.PerformClick();
                        this.uxComboBoxVaultAlias.SelectedItem = this.CurrentVaultAlias;
                        return false;
                    case PickVaultText:
                        var smd = new SubscriptionsManagerDialog();
                        if (smd.ShowDialog() != DialogResult.OK)
                        {
                            this.uxComboBoxVaultAlias.SelectedItem = this.CurrentVaultAlias;
                            return false;
                        }

                        // Add vault to temporary collection since it's new
                        this._tempVaultAliases[smd.CurrentVaultAlias.Alias] = smd.CurrentVaultAlias;
                        this.uxComboBoxVaultAlias.Items.Insert(this.uxComboBoxVaultAlias.Items.Count - 2, smd.CurrentVaultAlias);

                        if (this.uxComboBoxVaultAlias.SelectedItem == null)
                        {
                            this.uxComboBoxVaultAlias.SelectedItem = smd.CurrentVaultAlias;
                        }

                        // Set user alias and domain hint manually as they are not set from the assignment
                        if (this.uxComboBoxVaultAlias.SelectedItem is VaultAlias selectedVault)
                        {
                            selectedVault.UserAlias = smd.CurrentVaultAlias.UserAlias;
                            selectedVault.DomainHint = smd.CurrentVaultAlias.DomainHint;
                        }

                        break;
                }
            }

            // Only set CurrentVaultAlias if the selected item is actually a VaultAlias object
            if (this.uxComboBoxVaultAlias.SelectedItem is VaultAlias vaultAlias)
            {
                this.CurrentVaultAlias = vaultAlias;
                this.uxComboBoxVaultAlias.SelectedText = this.CurrentVaultAlias.Alias;
                // In some cases, the combobox will be blank. Setting the text on a blank combobox will null the selected item. So, always ensure the selecteditem is set when setting the selected text.
                this.uxComboBoxVaultAlias.SelectedItem = this.CurrentVaultAlias;
                this.uxComboBoxVaultAlias.ToolTipText = "Vault names: " + string.Join(", ", this.CurrentVaultAlias.VaultNames);
            }
            else
            {
                // Selected item is not a VaultAlias (probably a string like menu items), don't change CurrentVaultAlias
                this.uxComboBoxVaultAlias.ToolTipText = "";
            }

            bool itemSelected = null != this.CurrentVaultAlias;
            this.uxMenuItemRefresh.Enabled = itemSelected;
            return itemSelected;
        }

        private void SetCurrentVault()
        {
            this.CurrentVault = new Vault(Utils.FullPathToJsonFile(Settings.Default.VaultsJsonFileLocation), VaultAccessTypeEnum.ReadWrite, this.CurrentVaultAlias.VaultNames);
            // In case that subscription is chosen by the dialog, overwrite permissions taken from vaults.json
            if (this.CurrentVaultAlias.UserAlias != null || this.CurrentVault.VaultsConfig.Count == 0)
            {
                this.CurrentVault.VaultsConfig[this.CurrentVaultAlias.VaultNames[0]] = new VaultAccessType(
                    new VaultAccess[] { new VaultAccessUserInteractive(this.CurrentVaultAlias.DomainHint, this.CurrentVaultAlias.UserAlias) },
                    new VaultAccess[] { new VaultAccessUserInteractive(this.CurrentVaultAlias.DomainHint, this.CurrentVaultAlias.UserAlias) });
            }
        }

        private async void uxMenuItemRefresh_Click(object sender, EventArgs e)
        {
            using (var op = this.NewUxOperationWithProgress(this.uxMenuItemRefresh, this.uxComboBoxVaultAlias, this.uxButtonAdd, this.uxMenuItemAdd, this.uxButtonEdit, this.uxMenuItemEdit, this.uxButtonToggle, this.uxMenuItemToggle, this.uxButtonDelete, this.uxMenuItemDelete, this.uxImageSearch, this.uxTextBoxSearch, this.uxButtonShare, this.uxMenuItemShare, this.uxButtonFavorite, this.uxMenuItemFavorite))
            {
                try
                {
                    this.Text = Globals.AppName;
                    this.SetCurrentVault();
                    this.uxPropertyGridSecret.SelectedObjects = null;
                    this.uxListViewSecrets.AllowDrop = false;
                    this.uxListViewSecrets.RemoveAllItems();
                    this.uxListViewSecrets.Refresh();
                    this.RefreshItemsCount();
                    this.uxListViewSecrets.BeginUpdate();
                    int s = 0, c = 0;
                    Action updateCount = () => this.uxStatusLabelSecertsCount.Text = $"{s + c} secrets"; // We use delegate and Invoke() below to execute on the thread that owns the control
                    IEnumerable<SecretItem> secrets = Enumerable.Empty<SecretItem>();
                    IEnumerable<CertificateItem> certificates = Enumerable.Empty<CertificateItem>();
                    await op.Invoke("access",
                        async () => // List Secrets
                        {
                            this.CurrentVaultAlias.SecretsCollectionEnabled = false;
                            secrets = await this.CurrentVault.ListSecretsAsync(0, p =>
                            {
                                s = p;
                                this.Invoke(updateCount);
                            }, cancellationToken: op.CancellationToken);
                            this.CurrentVaultAlias.SecretsCollectionEnabled = true;
                        },
                        async () => // List Key Vault Certificates
                        {
                            this.CurrentVaultAlias.CertificatesCollectionEnabled = false;
                            certificates = await this.CurrentVault.ListCertificatesAsync(0, p =>
                            {
                                c = p;
                                this.Invoke(updateCount);
                            }, cancellationToken: op.CancellationToken);
                            this.CurrentVaultAlias.CertificatesCollectionEnabled = true;
                        }
                    );
                    foreach (var secret in secrets)
                    {
                        this.uxListViewSecrets.AddOrReplaceItem(new ListViewItemSecret(this, secret));
                    }

                    foreach (var cert in certificates)
                    {
                        // Remove "secret" (in fact this is a certifiacte) which was returned as part of ListSecretsAsync
                        this.uxListViewSecrets.AddOrReplaceItem(new ListViewItemCertificate(this, cert));
                    }
                }
                catch (OperationCanceledException) // User cancelled one of the list operations
                {
                    this.uxListViewSecrets.RemoveAllItems();
                    this.CurrentVaultAlias.SecretsCollectionEnabled = false;
                    this.CurrentVaultAlias.CertificatesCollectionEnabled = false;
                }
                catch
                {
                    this.uxListViewSecrets.RemoveAllItems();
                    throw; // Propogate the error and show error to user
                }
                finally
                {
                    // We failed to list from all collections, disable controls
                    if (!this.CurrentVaultAlias.SecretsCollectionEnabled && !this.CurrentVaultAlias.CertificatesCollectionEnabled)
                    {
                        UxOperation.ToggleControls(false, this.uxButtonAdd, this.uxMenuItemAdd, this.uxButtonEdit, this.uxMenuItemEdit, this.uxButtonToggle, this.uxMenuItemToggle, this.uxButtonDelete, this.uxMenuItemDelete, this.uxImageSearch, this.uxTextBoxSearch, this.uxButtonShare, this.uxMenuItemShare, this.uxButtonFavorite, this.uxMenuItemFavorite);
                    }
                    else // We were able to list from one or from both collections
                    {
                        this.Text += $" ({this.CurrentVault.AuthenticatedUserName})";
                        this.uxAddSecret.Visible = this.uxAddSecret2.Visible = this.uxAddCert.Visible = this.uxAddCert2.Visible = this.uxAddFile.Visible = this.uxAddFile2.Visible = this.CurrentVaultAlias.SecretsCollectionEnabled;
                        this.uxAddKVCert.Visible = this.uxAddKVCert2.Visible = this.CurrentVaultAlias.CertificatesCollectionEnabled;
                        this.uxListViewSecrets.AllowDrop = true;
                        this.uxListViewSecrets.RefreshGroupsHeader();
                    }

                    this.uxListViewSecrets.EndUpdate();
                    this.uxTimerSearchTextTypingCompleted_Tick(null, EventArgs.Empty); // Refresh search and items count
                }
            }
        }

        private void uxListViewSecrets_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool singleItemSelected = this.uxListViewSecrets.SelectedItems.Count == 1;
            bool manyItemsSelected = this.uxListViewSecrets.SelectedItems.Count >= 1;
            bool itemEnabled = this.uxListViewSecrets.FirstSelectedItem?.Enabled ?? false;
            bool favorite = this.uxListViewSecrets.FirstSelectedItem?.Favorite ?? false;
            this.uxButtonEdit.Enabled = this.uxButtonShare.Enabled = itemEnabled;
            this.uxMenuItemEdit.Enabled = this.uxMenuItemShare.Enabled = itemEnabled;
            this.uxButtonDelete.Enabled = this.uxMenuItemDelete.Enabled = this.uxButtonFavorite.Enabled = this.uxMenuItemFavorite.Enabled = manyItemsSelected;
            this.uxButtonToggle.Enabled = this.uxMenuItemToggle.Enabled = singleItemSelected;
            this.uxButtonToggle.Text = this.uxMenuItemToggle.Text = itemEnabled ? "Disabl&e" : "&Enable";
            this.uxButtonToggle.ToolTipText = this.uxMenuItemToggle.ToolTipText = itemEnabled ? "Disable item" : "Enable item";
            this.uxMenuItemToggle.Text = this.uxButtonToggle.Text + "...";
            this.uxButtonFavorite.Checked = this.uxMenuItemFavorite.Checked = favorite;
            this.uxButtonFavorite.ToolTipText = this.uxMenuItemFavorite.ToolTipText = favorite ? "Remove item(s) from favorites group" : "Add item(s) to favorites group";
            this.uxPropertyGridSecret.SelectedObjects = this.uxListViewSecrets.SelectedItems?.Cast<ListViewItemBase>().ToArray();
            this.RefreshItemsCount();
        }

        private void uxListViewSecrets_KeyDown(object sender, KeyEventArgs e)
        {
            this._keyDownOccured = true; // Prevents from 'global' KeyUp event, basically key down happened in the other app
        }

        private void uxListViewSecrets_KeyUp(object sender, KeyEventArgs e)
        {
            if (!this._keyDownOccured)
            {
                return;
            }

            this._keyDownOccured = false;
            switch (e.KeyCode)
            {
                case Keys.F1:
                    this.uxButtonHelp.PerformClick();
                    return;
                case Keys.F5:
                    this.uxMenuItemRefresh.PerformClick();
                    return;
                case Keys.Insert:
                    this.uxButtonAdd.PerformClick();
                    return;
                case Keys.Enter:
                    this.uxButtonEdit.PerformClick();
                    return;
            }

            if (!e.Control)
            {
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.A:
                    foreach (ListViewItemBase item in this.uxListViewSecrets.Items)
                    {
                        item.Selected = true;
                    }

                    return;
                case Keys.F:
                    this.uxTextBoxSearch.Focus();
                    return;
            }
        }

        private void uxButtonAdd_Click(object sender, EventArgs e)
        {
            (sender as ToolStripDropDownItem)?.ShowDropDown();
        }

        private FileInfo GetFileInfo(object sender, EventArgs e)
        {
            FileInfo fi = null;
            if (e is AddFileEventArgs) // File was dropped
            {
                fi = new FileInfo((e as AddFileEventArgs).FileName);
            }
            else
            {
                this.uxOpenFileDialog.FilterIndex = sender == this.uxAddCertFromFile || sender == this.uxAddCertFromFile2 || sender == this.uxAddKVCertFromFile || sender == this.uxAddKVCertFromFile2 ? ContentType.Pkcs12.ToFilterIndex() : ContentType.None.ToFilterIndex();
                if (this.uxOpenFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                fi = new FileInfo(this.uxOpenFileDialog.FileName);
            }

            if (fi.Length > Consts.MB)
            {
                MessageBox.Show($"File {fi.FullName} size is {fi.Length:N0} bytes. Maximum file size allowed for secret value (before compression) is 1 MB.", Globals.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            return fi;
        }

        private void AddOrReplaceItemInListView(ListViewItemBase newItem, ListViewItemBase oldItem = null)
        {
            try
            {
                this.uxListViewSecrets.BeginUpdate();
                if (null != oldItem)
                {
                    this.uxListViewSecrets.Items.Remove(oldItem); // Rename flow
                }

                this.uxListViewSecrets.AddOrReplaceItem(newItem); // Overwrite flow
                this.uxTimerSearchTextTypingCompleted_Tick(null, EventArgs.Empty); // Refresh search
                newItem?.RefreshAndSelect();
                this.uxListViewSecrets.RefreshGroupsHeader();
                this.RefreshItemsCount();
            }
            finally
            {
                this.uxListViewSecrets.EndUpdate();
            }
        }

        private async void uxMenuItemAddSecret_Click(object sender, EventArgs e)
        {
            SecretDialog nsDlg = null;
            // Add secret
            using (var dtf = new DeleteTempFileInfo())
            {
                if (sender == this.uxAddSecret || sender == this.uxAddSecret2)
                {
                    nsDlg = new SecretDialog(this);
                }

                // Add certificate from file or configuration file
                if (sender == this.uxAddCertFromFile || sender == this.uxAddCertFromFile2 || sender == this.uxAddFile || sender == this.uxAddFile2)
                {
                    dtf.FileInfoObject = this.GetFileInfo(sender, e);
                    if (dtf.FileInfoObject == null)
                    {
                        return;
                    }

                    nsDlg = new SecretDialog(this, dtf.FileInfoObject);
                }

                // Add certificate from store
                if (sender == this.uxAddCertFromUserStore || sender == this.uxAddCertFromUserStore2 || sender == this.uxAddCertFromMachineStore || sender == this.uxAddCertFromMachineStore2)
                {
                    var cert = Utils.SelectCertFromStore(StoreName.My, sender == this.uxAddCertFromUserStore || sender == this.uxAddCertFromUserStore2 ? StoreLocation.CurrentUser : StoreLocation.LocalMachine, this.CurrentVaultAlias.Alias, this.Handle);
                    if (cert == null)
                    {
                        return;
                    }

                    nsDlg = new SecretDialog(this, cert);
                }

                // DialogResult.Cancel is when user clicked cancel during password prompt from the ctor(), if OK was clicked, check for duplication by Name and Md5
                if (nsDlg != null && nsDlg.DialogResult != DialogResult.Cancel && nsDlg.ShowDialog() == DialogResult.OK && ListViewItemBase.VerifyDuplication(this, null, nsDlg.PropertyObject))
                {
                    using (var op = this.NewUxOperationWithProgress(this.uxButtonAdd, this.uxMenuItemAdd))
                    {
                        ListViewItemSecret lvis = null;
                        await op.Invoke("add secret to", async () => lvis = await ListViewItemSecret.NewAsync(this, nsDlg.PropertyObject, op.CancellationToken));
                        this.AddOrReplaceItemInListView(lvis);
                    }
                }
            }
        }

        private async void uxMenuItemAddKVCertificate_Click(object sender, EventArgs e)
        {
            CertificateDialog certDlg = null;
            // Add certificate
            using (var dtf = new DeleteTempFileInfo())
            {
                // Add certificate from file
                if (sender == this.uxAddKVCertFromFile || sender == this.uxAddKVCertFromFile2)
                {
                    dtf.FileInfoObject = this.GetFileInfo(sender, e);
                    if (dtf.FileInfoObject == null)
                    {
                        return;
                    }

                    certDlg = new CertificateDialog(this, dtf.FileInfoObject);
                }

                // Add certificate from store
                if (sender == this.uxAddKVCertFromUserStore || sender == this.uxAddKVCertFromMachineStore || sender == this.uxAddKVCertFromUserStore2 || sender == this.uxAddKVCertFromMachineStore2)
                {
                    var cert = Utils.SelectCertFromStore(StoreName.My, sender == this.uxAddKVCertFromUserStore || sender == this.uxAddKVCertFromUserStore2 ? StoreLocation.CurrentUser : StoreLocation.LocalMachine, this.CurrentVaultAlias.Alias, this.Handle);
                    if (cert == null)
                    {
                        return;
                    }

                    certDlg = new CertificateDialog(this, cert);
                }

                // DialogResult.Cancel is when user clicked cancel during password prompt from the ctor(), if OK was clicked, check for duplication by Name and Md5
                if (certDlg != null && certDlg.DialogResult != DialogResult.Cancel && certDlg.ShowDialog() == DialogResult.OK && ListViewItemBase.VerifyDuplication(this, null, certDlg.PropertyObject))
                {
                    using (var op = this.NewUxOperationWithProgress(this.uxButtonAdd, this.uxMenuItemAdd))
                    {
                        ListViewItemCertificate lvic = null;
                        await op.Invoke("add certificate to", async () => lvic = await ListViewItemCertificate.NewAsync(this, certDlg.PropertyObject, op.CancellationToken));
                        this.AddOrReplaceItemInListView(lvic);
                    }
                }
            }
        }

        private async void uxButtonEdit_Click(object sender, EventArgs e)
        {
            var item = this.uxListViewSecrets.FirstSelectedItem;
            if (item == null)
            {
                return;
            }

            if (!item.Active && MessageBox.Show($"'{item.Name}' {item.Kind} is not active or expired. In order to view or edit {item.Kind}, {Globals.AppName} must change the expiration times of '{item.Name}'. Are you sure you want to change Valid from time (UTC): '{Utils.NullableDateTimeToString(item.NotBefore)}' and Valid until time (UTC): '{Utils.NullableDateTimeToString(item.Expires)}' to one year from now?\n\nNote: You will be able to change back the expiration times in the Edit dialog if needed.",
                    Globals.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                using (var op = this.NewUxOperationWithProgress(this.uxButtonEdit, this.uxMenuItemEdit))
                {
                    ListViewItemBase newItem = null;
                    await op.Invoke($"update {item.Kind} in", async () => newItem = await item.ResetExpirationAsync(op.CancellationToken));
                    this.AddOrReplaceItemInListView(newItem, item);
                    item = newItem;
                }

                ;
            }

            if (item.Enabled && item.Active)
            {
                IEnumerable<object> versions = null;
                using (var op = this.NewUxOperationWithProgress(this.uxButtonEdit, this.uxMenuItemEdit))
                {
                    await op.Invoke($"get {item.Kind} from", async () => { versions = await item.GetVersionsAsync(op.CancellationToken); });
                }

                dynamic editDlg = item.GetEditDialog(item.Name, versions);
                // If OK was clicked, check for duplication by Name and Md5
                if (editDlg.ShowDialog() == DialogResult.OK && ListViewItemBase.VerifyDuplication(this, item.Name, editDlg.PropertyObject))
                {
                    using (var op = this.NewUxOperationWithProgress(this.uxButtonEdit, this.uxMenuItemEdit))
                    {
                        ListViewItemBase newItem = null;
                        await op.Invoke($"update {item.Kind} in", async () => newItem = await item.UpdateAsync(editDlg.OriginalObject, editDlg.PropertyObject, op.CancellationToken));
                        this.AddOrReplaceItemInListView(newItem, item);
                    }

                    ;
                }
            }
        }

        private async void uxButtonToggle_Click(object sender, EventArgs e)
        {
            var item = this.uxListViewSecrets.FirstSelectedItem;
            if (null != item)
            {
                string action = item.Enabled ? "disable" : "enable";
                if (MessageBox.Show($"Are you sure you want to {action} {item.Kind} '{item.Name}'?", Globals.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    using (var op = this.NewUxOperationWithProgress(this.uxButtonToggle, this.uxMenuItemToggle))
                    {
                        ListViewItemBase lvib = null;
                        await op.Invoke($"update {item.Kind} in", async () => lvib = await item.ToggleAsync(op.CancellationToken));
                        this.AddOrReplaceItemInListView(lvib, item);
                    }
                }
            }
        }

        private async void uxButtonDelete_Click(object sender, EventArgs e)
        {
            if (this.uxListViewSecrets.SelectedItems.Count > 0)
            {
                string itemNames = string.Join(", ", from item in this.uxListViewSecrets.SelectedItems.Cast<ListViewItem>() select item.Name);
                if (MessageBox.Show($"Are you sure you want to delete {this.uxListViewSecrets.SelectedItems.Count} item(s) with the following name(s)?\n{itemNames}\n\nWarning: This operation can not be undone!", Globals.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    using (var op = this.NewUxOperationWithProgress(this.uxButtonDelete, this.uxMenuItemDelete))
                    {
                        foreach (ListViewItemBase lvi in this.uxListViewSecrets.SelectedItems)
                        {
                            await op.Invoke("delete item in", async () => await lvi.DeleteAsync(op.CancellationToken));
                            this.AddOrReplaceItemInListView(null, lvi);
                        }
                    }
                }
            }
        }

        private void uxTimerSearchTextTypingCompleted_Tick(object sender, EventArgs e)
        {
            this.uxTimerSearchTextTypingCompleted.Stop();
            var ex = this.uxListViewSecrets.FindItemsWithText(this.uxTextBoxSearch.Text);
            this.uxTextBoxSearch.ForeColor = ex == null ? DefaultForeColor : Color.Red;
            this.uxTextBoxSearch.ToolTipText = ex == null ? this.uxImageSearch.ToolTipText : $"Regular expression error during {ex.Message}";
            this.RefreshItemsCount();
        }

        private void uxTextBoxSearch_TextChanged(object sender, EventArgs e)
        {
            this.uxTimerSearchTextTypingCompleted.Stop(); // Wait for user to finish the typing in a text box
            this.uxTimerSearchTextTypingCompleted.Start();
        }

        private async void uxButtonCopy_Click(object sender, EventArgs e)
        {
            var item = this.uxListViewSecrets.FirstSelectedItem;
            if (null != item)
            {
                using (var op = this.NewUxOperationWithProgress(this.uxButtonCopy, this.uxMenuItemCopy))
                {
                    PropertyObject po = null;
                    await op.Invoke($"get {item.Kind} from", async () => po = await item.GetAsync(op.CancellationToken));
                    po.CopyToClipboard(false); // Always execute on single thread apartment (STA) - UI thread, because of OLE limitations
                }
            }
        }

        private void uxButtonCopyLink_Click(object sender, EventArgs e)
        {
            var item = this.uxListViewSecrets.FirstSelectedItem;
            if (null != item)
            {
                using (var op = this.NewUxOperation(this.uxButtonCopyLink, this.uxMenuItemCopyLink))
                {
                    Utils.ClipboardSetHyperlink(item.Link, item.Name);
                }
            }
        }

        private async void uxButtonSave_Click(object sender, EventArgs e)
        {
            var item = this.uxListViewSecrets.FirstSelectedItem;
            if (null != item)
            {
                PropertyObject po = null;
                using (var op = this.NewUxOperationWithProgress(this.uxButtonSave, this.uxMenuItemSave))
                {
                    await op.Invoke($"get {item.Kind} from", async () => { po = await item.GetAsync(op.CancellationToken); });
                }

                this.uxSaveFileDialog.FileName = po.GetFileName();
                this.uxSaveFileDialog.DefaultExt = po.GetContentType().ToExtension();
                this.uxSaveFileDialog.FilterIndex = po.GetContentType().ToFilterIndex();
                if (this.uxSaveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    po.SaveToFile(this.uxSaveFileDialog.FileName);
                }
            }
        }

        private void uxButtonExportToTsv_Click(object sender, EventArgs e)
        {
            using (var op = this.NewUxOperation(this.uxButtonExportToTsv))
            {
                this.uxSaveFileDialog.FileName = $"{this.CurrentVaultAlias.Alias}_{DateTime.Now.ToString("yyyy-MM-dd")}";
                this.uxSaveFileDialog.DefaultExt = ".tsv";
                this.uxSaveFileDialog.FilterIndex = ContentType.Tsv.ToFilterIndex();
                if (this.uxSaveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    this.uxListViewSecrets.ExportToTsv(this.uxSaveFileDialog.FileName);
                }
            }
        }

        private void uxButtonFavorite_Click(object sender, EventArgs e)
        {
            if (this.uxListViewSecrets.SelectedItems.Count > 0)
            {
                using (var op = this.NewUxOperationWithProgress(this.uxButtonFavorite, this.uxMenuItemFavorite))
                {
                    this.uxListViewSecrets.ToggleSelectedItemsToFromFavorites();
                    this.SaveSettings();
                }

                this.uxListViewSecrets_SelectedIndexChanged(null, EventArgs.Empty);
            }
        }

        private void uxButtonSettings_Click(object sender, EventArgs e)
        {
            using (var op = this.NewUxOperation(this.uxButtonSettings))
            {
                var dlg = new SettingsDialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    this.ApplySettings();
                }
            }
        }

        private void uxButtonHelp_Click(object sender, EventArgs e)
        {
            using (var op = this.NewUxOperation(this.uxButtonHelp))
            {
                ProcessStartInfo sInfo = new ProcessStartInfo { FileName = Globals.GitHubIssuesUrl, UseShellExecute = true };
                Process.Start(sInfo);
            }
        }

        #region Drag & Drop

        // Flags to indicate if CTRL and SHIFT keys were down during start of the drag
        private bool _ctrlKeyPressed;
        private bool _shiftKeyPressed;

        private async void uxListViewSecrets_ItemDrag(object sender, ItemDragEventArgs e)
        {
            using (var op = this.NewUxOperation(this.uxButtonSave, this.uxMenuItemSave))
            {
                this._ctrlKeyPressed = (ModifierKeys & Keys.Control) != 0;
                this._shiftKeyPressed = (ModifierKeys & Keys.Shift) != 0;
                List<string> filesList = new List<string>();
                foreach (var item in this.uxListViewSecrets.SelectedItems.Cast<ListViewItemBase>())
                {
                    PropertyObject po = null;
                    await op.Invoke("get item from", async () => po = await item.GetAsync(op.CancellationToken));
                    // Pick .kv-secret or .kv-certificate or .url extension if CTRL and SHIFT are pressed
                    var filename = po.Name + (this._ctrlKeyPressed & this._shiftKeyPressed ? ContentType.KeyVaultLink.ToExtension() : this._ctrlKeyPressed ? po.GetKeyVaultFileExtension() : po.GetContentType().ToExtension());
                    var fullName = Path.Combine(Path.GetTempPath(), filename);
                    po.SaveToFile(fullName);
                    filesList.Add(fullName);
                }

                var dataObject = new DataObject(DataFormats.FileDrop, filesList.ToArray());
                this.uxListViewSecrets.DoDragDrop(dataObject, DragDropEffects.Move);
            }
        }

        private void uxListViewSecrets_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = false;
            Cursor.Current = this._ctrlKeyPressed & this._shiftKeyPressed ? this._moveLinkCursor : this._ctrlKeyPressed ? this._moveSecretCursor : this._moveValueCursor;
        }

        private void uxListViewSecrets_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = this.CurrentVault != null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void uxListViewSecrets_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // Since we are about to show modal dialog(s), we release the caller (other Vault Explorer instance) by calling ourself via BeginInvoke
                this.BeginInvoke(new ProcessDropedFilesDelegate(this.ProcessDropedFiles), string.Join("|", files));
            }
        }

        private delegate void ProcessDropedFilesDelegate(string files);

        private void ProcessDropedFiles(string files)
        {
            foreach (string file in files.Split('|'))
            {
                FileInfo fi = new FileInfo(file);
                switch (ContentTypeUtils.FromExtension(fi.Extension))
                {
                    case ContentType.KeyVaultCertificate:
                        this.uxMenuItemAddKVCertificate_Click(this.uxAddKVCertFromFile, new AddFileEventArgs(file));
                        break;
                    case ContentType.KeyVaultSecret:
                    default:
                        this.uxMenuItemAddSecret_Click(this.uxAddFile, new AddFileEventArgs(file));
                        break;
                }
            }
        }

        #endregion
    }
}