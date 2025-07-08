namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System.Windows.Forms;

    public class ListViewItemSubscription : ListViewItem
    {
        public readonly Subscription Subscription;

        public ListViewItemSubscription(Subscription s) : base(s.DisplayName)
        {
            this.Subscription = s;
            this.Name = s.DisplayName;
            this.SubItems.Add(s.SubscriptionId.ToString());
            this.ToolTipText = $"State: {s.State}";
            this.ImageIndex = 0;
        }
    }
}