namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using Newtonsoft.Json;

    [JsonObject]
    public class Subscription
    {
        public string Id { get; set; }
        public Guid SubscriptionId { get; set; }
        public string DisplayName { get; set; }
        public string State { get; set; }
        public string AuthorizationSource { get; set; }
    }
}