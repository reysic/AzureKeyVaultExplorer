namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using Newtonsoft.Json;

    [JsonObject]
    public class SubscriptionsResponse
    {
        [JsonProperty(PropertyName = "value")]
        public Subscription[] Subscriptions { get; set; }
    }
}