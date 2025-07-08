namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    public class ListViewItemVault : ListViewItem
    {
        // https://azure.microsoft.com/en-us/documentation/articles/guidance-naming-conventions/
        private static readonly Regex s_resourceNameRegex = new Regex(@".*\/resourceGroups\/(?<GroupName>[a-zA-Z0-9_\-\.]{1,64})\/", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public readonly Azure.Management.KeyVault.Models.Resource Vault;
        public readonly string GroupName;

        public ListViewItemVault(Azure.Management.KeyVault.Models.Resource vault) : base(vault.Name)
        {
            this.Vault = vault;
            this.Name = vault.Name;
            this.GroupName = s_resourceNameRegex.Match(vault.Id).Groups["GroupName"].Value;
            this.SubItems.Add(this.GroupName);
            this.ToolTipText = $"Location: {vault.Location}";
            this.ImageIndex = 1;
        }
    }
}