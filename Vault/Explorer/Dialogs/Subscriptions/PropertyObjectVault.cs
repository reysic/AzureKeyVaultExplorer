namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.ComponentModel;
    using Microsoft.Azure.Management.KeyVault.Models;
    using Microsoft.Vault.Explorer.Model.Collections;

    public class PropertyObjectVault
    {
        private readonly Subscription _subscription;
        private readonly string _resourceGroupName;
        private readonly Vault _vault;

        public PropertyObjectVault(Subscription s, string resourceGroupName, Vault vault)
        {
            this._subscription = s;
            this._resourceGroupName = resourceGroupName;
            this._vault = vault;
            this.Tags = new ObservableTagItemsCollection();
            if (null != this._vault.Tags)
            {
                foreach (var kvp in this._vault.Tags)
                {
                    this.Tags.Add(new TagItem(kvp));
                }
            }

            this.AccessPolicies = new ObservableAccessPoliciesCollection();
            int i = -1;
            foreach (var ape in this._vault.Properties.AccessPolicies)
            {
                this.AccessPolicies.Add(new AccessPolicyEntryItem(++i, ape));
            }
        }

        [DisplayName("Name")]
        [ReadOnly(true)]
        public string Name => this._vault.Name;

        [DisplayName("Location")]
        [ReadOnly(true)]
        public string Location => this._vault.Location;

        [DisplayName("Uri")]
        [ReadOnly(true)]
        public string Uri => this._vault.Properties.VaultUri;

        [DisplayName("Subscription Name")]
        [ReadOnly(true)]
        public string SubscriptionName => this._subscription.DisplayName;

        [DisplayName("Subscription Id")]
        [ReadOnly(true)]
        public Guid SubscriptionId => this._subscription.SubscriptionId;

        [DisplayName("Resource Group Name")]
        [ReadOnly(true)]
        public string ResourceGroupName => this._resourceGroupName;

        [DisplayName("Custom Tags")]
        [ReadOnly(true)]
        public ObservableTagItemsCollection Tags { get; private set; }

        [DisplayName("Sku")]
        [ReadOnly(true)]
        public SkuName Sku => this._vault.Properties.Sku.Name;

        [DisplayName("Access Policies")]
        [ReadOnly(true)]
        [TypeConverter(typeof(ExpandableCollectionObjectConverter))]
        public ObservableAccessPoliciesCollection AccessPolicies { get; }
    }
}