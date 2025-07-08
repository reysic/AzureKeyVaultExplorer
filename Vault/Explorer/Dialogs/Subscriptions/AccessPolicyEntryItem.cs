namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.ComponentModel;
    using System.Drawing.Design;
    using Microsoft.Azure.Management.KeyVault.Models;
    using Newtonsoft.Json;

    [Editor(typeof(ExpandableObjectConverter), typeof(UITypeEditor))]
    public class AccessPolicyEntryItem
    {
        private static readonly string[] EmptyList = new string[] { };
        private readonly AccessPolicyEntry _ape;

        public AccessPolicyEntryItem(int index, AccessPolicyEntry ape)
        {
            this.Index = index;
            this._ape = ape;
        }

        [JsonIgnore]
        public int Index { get; }

        [Description("Application ID of the client making request on behalf of a principal")]
        public Guid? ApplicationId => this._ape.ApplicationId;

        [Description("Object ID of the principal")]
        public Guid ObjectId => Guid.Parse(this._ape.ObjectId);

        [Description("Permissions to keys")]
        public string PermissionsToKeys => string.Join(",", this._ape.Permissions.Keys ?? EmptyList);

        [Description("Permissions to secrets")]
        public string PermissionsToSecrets => string.Join(",", this._ape.Permissions.Secrets ?? EmptyList);

        [Description("Permissions to certificates")]
        public string PermissionsToCertificates => string.Join(",", this._ape.Permissions.Certificates ?? EmptyList);

        [Description("Tenant ID of the principal")]
        public Guid TenantId => this._ape.TenantId;

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}