namespace Microsoft.Vault.Explorer.Controls.MenuItems
{
    using System;
    using System.Linq;
    using System.Windows.Forms;
    using Microsoft.Vault.Explorer.Controls.Lists;

    public class TagMenuItem : ToolStripMenuItem, IComparable, IComparable<TagMenuItem>
    {
        public readonly ListViewSecrets ListView;
        public int Count;

        public TagMenuItem(string tag, ListViewSecrets listView) : base(tag)
        {
            this.Name = tag;
            this.Count = 0;
            this.ListView = listView;
        }

        public override string Text
        {
            get { return $"{this.Name} ({this.Count})"; }
            set { base.Text = value; }
        }

        public override int GetHashCode() => this.Name.GetHashCode();

        public override bool Equals(object obj) => this.Equals((TagMenuItem)obj);

        public bool Equals(TagMenuItem tag)
        {
            if (null == tag)
            {
                return false;
            }

            return this.Name == tag.Name;
        }

        public int CompareTo(object obj) => this.CompareTo((TagMenuItem)obj);

        public int CompareTo(TagMenuItem other)
        {
            if (null == other)
            {
                return -1;
            }

            return string.Compare(this.Name, other.Name);
        }

        protected override void OnClick(EventArgs e)
        {
            this.Checked = !this.Checked;
            base.OnClick(e);
            if (this.Checked)
            {
                this.ListView.Columns.Add(new ColumnHeader { Name = this.Name, Text = this.Name, Width = 200 });
            }
            else
            {
                this.ListView.Columns.RemoveByKey(this.Name);
            }

            foreach (var item in this.ListView.Items.Cast<ListViewItemBase>())
            {
                item.RepopulateSubItems();
            }
        }
    }
}