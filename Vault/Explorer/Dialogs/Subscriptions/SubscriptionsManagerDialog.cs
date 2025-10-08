// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Azure.Management.KeyVault;
    using Microsoft.Identity.Client;
    using Microsoft.Rest;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;
    using Settings = Microsoft.Vault.Explorer.Settings;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    public partial class SubscriptionsManagerDialog : Form
    {
        private const string ApiVersion = "api-version=2016-07-01";
        private const string ManagmentEndpoint = "https://management.azure.com/";
        private const string AddAccountText = "Add New Account";

        private AccountItem _currentAccountItem;
        private AuthenticationResult _currentAuthResult;
        private KeyVaultManagementClient _currentKeyVaultMgmtClient;
        private readonly HttpClient _httpClient;

        public VaultAlias CurrentVaultAlias { get; private set; }

        public SubscriptionsManagerDialog()
        {
            this.InitializeComponent();
            this._httpClient = new HttpClient();

            // Create Default accounts based on domain hints and aliases.
            bool hasPreConfiguredAccounts = false;
            foreach (string userAccountName in Settings.Default.UserAccountNamesList)
            {
                string[] accounts = userAccountName.Split('@');
                if (accounts.Length < 2)
                {
                    continue;
                }

                this.uxComboBoxAccounts.Items.Add(new AccountItem(accounts[1], accounts[0]));
                hasPreConfiguredAccounts = true;
            }

            this.uxComboBoxAccounts.Items.Add(AddAccountText);

            // Only auto-select if we have pre-configured accounts, otherwise let user choose
            if (hasPreConfiguredAccounts)
            {
                this.uxComboBoxAccounts.SelectedIndex = 0;
            }
            else
            {
                // No pre-configured accounts, don't auto-select anything
                this.uxComboBoxAccounts.SelectedIndex = -1;
                this.uxComboBoxAccounts.Text = "Select an account or add new...";
            }
        }

        private UxOperation NewUxOperationWithProgress(params ToolStripItem[] controlsToToggle) => new UxOperation(null, this.uxStatusLabel, this.uxProgressBar, this.uxButtonCancelOperation, controlsToToggle);

        private async void uxComboBoxAccounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (this.uxComboBoxAccounts.SelectedItem)
            {
                case null:
                    return;

                case AddAccountText:
                    this.AddNewAccount();
                    break;

                case AccountItem account:
                    // Authenticate into selected account
                    this._currentAccountItem = account;
                    await this.GetAuthenticationTokenAsync();
                    if (this._currentAuthResult.Account != null)
                    {
                        this._currentAccountItem.UserAlias = this._currentAuthResult.Account.Username.Split('@')[0];
                    }

                    break;

                default:
                    return;
            }

            if (this._currentAuthResult == null)
            {
                return;
            }

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts))
            {
                this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this._currentAuthResult.AccessToken);
                var hrm = await this._httpClient.GetAsync($"{ManagmentEndpoint}subscriptions?{ApiVersion}", op.CancellationToken);
                var json = await hrm.Content.ReadAsStringAsync();
                var subs = JsonConvert.DeserializeObject<SubscriptionsResponse>(json);

                this.uxListViewSubscriptions.Items.Clear();
                this.uxListViewVaults.Items.Clear();
                this.uxPropertyGridVault.SelectedObject = null;
                foreach (var s in subs.Subscriptions)
                {
                    this.uxListViewSubscriptions.Items.Add(new ListViewItemSubscription(s));
                }
            }
        }

        private async void uxListViewSubscriptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListViewItemSubscription s = this.uxListViewSubscriptions.SelectedItems.Count > 0 ? (ListViewItemSubscription)this.uxListViewSubscriptions.SelectedItems[0] : null;
            if (null == s)
            {
                return;
            }

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts))
            {
                var tvcc = new TokenCredentials(this._currentAuthResult.AccessToken);
                this._currentKeyVaultMgmtClient = new KeyVaultManagementClient(tvcc) { SubscriptionId = s.Subscription.SubscriptionId.ToString() };
                var vaults = await this._currentKeyVaultMgmtClient.Vaults.ListAsync(null, op.CancellationToken);
                this.uxListViewVaults.Items.Clear();
                foreach (var v in vaults)
                {
                    this.uxListViewVaults.Items.Add(new ListViewItemVault(v));
                }
            }
        }

        private async void uxListViewVaults_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListViewItemSubscription s = this.uxListViewSubscriptions.SelectedItems.Count > 0 ? (ListViewItemSubscription)this.uxListViewSubscriptions.SelectedItems[0] : null;
            ListViewItemVault v = this.uxListViewVaults.SelectedItems.Count > 0 ? (ListViewItemVault)this.uxListViewVaults.SelectedItems[0] : null;
            this.uxButtonOK.Enabled = false;
            if (null == s || null == v)
            {
                return;
            }

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts))
            {
                var vault = await this._currentKeyVaultMgmtClient.Vaults.GetAsync(v.GroupName, v.Name);
                this.uxPropertyGridVault.SelectedObject = new PropertyObjectVault(s.Subscription, v.GroupName, vault);
                this.uxButtonOK.Enabled = true;

                this.CurrentVaultAlias = new VaultAlias(v.Name, new[] { v.Name }, new[] { "Custom" })
                {
                    DomainHint = this._currentAccountItem.DomainHint,
                    UserAlias = this._currentAccountItem.UserAlias,
                    IsNew = true, // Mark as new since it's being added from SubscriptionsManagerDialog
                };
            }
        }


        private async void AddNewAccount()
        {
            try
            {
                // For new account, use "common" as domain hint to let Azure AD determine the tenant
                this._currentAccountItem = new AccountItem("common");
                await this.GetAuthenticationTokenAsync();

                if (string.IsNullOrEmpty(this._currentAuthResult.Account?.Username))
                {
                    MessageBox.Show("Authentication did not return a user name. Please try again.", "Authentication Problem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get new user account and add it to default settings
                string userAccountName = this._currentAuthResult.Account.Username;
                string[] userLogin = userAccountName.Split('@');
                this._currentAccountItem.UserAlias = userLogin[0];
                this._currentAccountItem.DomainHint = userLogin[1];
                if (!Settings.Default.AddUserAccountName(userAccountName))
                {
                    MessageBox.Show($"The user name {userAccountName} already exists.", "Username exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Add the new account to the dropdown and select it
                var newAccountItem = new AccountItem(this._currentAccountItem.DomainHint, this._currentAccountItem.UserAlias);
                this.uxComboBoxAccounts.Items.Insert(0, newAccountItem);
                this.uxComboBoxAccounts.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Authentication failed: {ex.Message}", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Reset selection to allow user to try again
                this.uxComboBoxAccounts.SelectedIndex = -1;
                this.uxComboBoxAccounts.Text = "Select an account or add new...";
            }
        }

        // Attempt to authenticate with current account.
        private async Task GetAuthenticationTokenAsync()
        {
            VaultAccessUserInteractive vaui = new VaultAccessUserInteractive(this._currentAccountItem.DomainHint, this._currentAccountItem.UserAlias);
            string[] scopes = VaultAccess.ConvertResourceToScopes(ManagmentEndpoint);
            this._currentAuthResult = await vaui.AcquireTokenAsync(scopes, this._currentAccountItem.UserAlias);
        }
    }
}