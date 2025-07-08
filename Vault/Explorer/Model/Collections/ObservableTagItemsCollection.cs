namespace Microsoft.Vault.Explorer.Model.Collections
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing.Design;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Library;

    [Editor(typeof(ExpandableCollectionEditor<ObservableTagItemsCollection, TagItem>), typeof(UITypeEditor))]
    public class ObservableTagItemsCollection : ObservableCustomCollection<TagItem>
    {
        public ObservableTagItemsCollection()
        {
        }

        public ObservableTagItemsCollection(IEnumerable<TagItem> collection) : base(collection)
        {
        }

        protected override PropertyDescriptor GetPropertyDescriptor(TagItem item) => new ReadOnlyPropertyDescriptor(item.Name, item.Value);

        protected override void InsertItem(int index, TagItem item)
        {
            if (this.Count >= Consts.MaxNumberOfTags)
            {
                throw new ArgumentOutOfRangeException("Tags.Count", $"Too many tags, maximum number of tags for secret is only {Consts.MaxNumberOfTags}");
            }

            base.InsertItem(index, item);
        }
    }
}