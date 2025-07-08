namespace Microsoft.Vault.Explorer.Model.Collections
{
    using System.ComponentModel;
    using Microsoft.Azure.KeyVault.Models;

    [DefaultProperty("Type")]
    [Description("Action and its trigger that will be performed by Key Vault over the lifetime of a certificate.")]
    public class LifetimeActionItem
    {
        [Category("Action")]
        public ActionType? Type { get; set; }

        [Category("Trigger")]
        public int? DaysBeforeExpiry { get; set; }

        [Category("Trigger")]
        public int? LifetimePercentage { get; set; }

        public override string ToString() => this.Type?.ToString();
    }
}