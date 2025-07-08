namespace Microsoft.Vault.Explorer.Model.Collections
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing.Design;
    using Microsoft.Vault.Explorer.Common;

    [Editor(typeof(ExpandableCollectionEditor<ObservableLifetimeActionsCollection, LifetimeActionItem>), typeof(UITypeEditor))]
    public class ObservableLifetimeActionsCollection : ObservableCustomCollection<LifetimeActionItem>
    {
        public ObservableLifetimeActionsCollection()
        {
        }

        public ObservableLifetimeActionsCollection(IEnumerable<LifetimeActionItem> collection) : base(collection)
        {
        }

        protected override PropertyDescriptor GetPropertyDescriptor(LifetimeActionItem item) =>
            new ReadOnlyPropertyDescriptor(item.ToString(), $"DaysBeforeExpiry={Utils.NullableIntToString(item.DaysBeforeExpiry)}, LifetimePercentage={Utils.NullableIntToString(item.LifetimePercentage)}");
    }
}