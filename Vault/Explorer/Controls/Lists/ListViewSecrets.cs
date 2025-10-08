// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.Lists
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;
    using Microsoft.Vault.Explorer.Controls.MenuItems;

    public partial class ListViewSecrets : ListView
    {
        public const int FirstCustomColumnIndex = 4;

        public int SortingColumn { get; set; }

        public ListViewSecrets()
        {
            this.InitializeComponent();
            this.ListViewItemSorter = new ListViewSecretsSorter(this);
            this.DoubleBuffered = true;
            this._tags = new Dictionary<string, TagMenuItem>();
        }

        public ListViewItemBase FirstSelectedItem => this.SelectedItems.Count > 0 ? this.SelectedItems[0] as ListViewItemBase : null;

        public int SearchResultsCount => this.Groups[ListViewItemBase.SearchResultsGroup].Items.Count;

        public void RefreshGroupsHeader()
        {
            foreach (var g in this.Groups.Cast<ListViewGroup>())
            {
                g.Header = $"{g.Name} ({g.Items.Count})";
            }
        }

        private void RemoveTags(IDictionary<string, string> tags)
        {
            if (null == tags)
            {
                return;
            }

            foreach (string t in tags.Keys)
            {
                if (false == this._tags.ContainsKey(t))
                {
                    continue;
                }

                this._tags[t].Count--;
                if (this._tags[t].Count < 0)
                {
                    this._tags.Remove(t);
                }
            }
        }

        private void AddTags(IDictionary<string, string> tags)
        {
            if (null == tags)
            {
                return;
            }

            foreach (string t in tags.Keys)
            {
                var tag = this._tags.ContainsKey(t) ? this._tags[t] : new TagMenuItem(t, this);
                tag.Count++;
                this._tags[t] = tag;
            }
        }

        public void AddOrReplaceItem(ListViewItemBase item)
        {
            if (null == item)
            {
                return;
            }

            if (this.Items.ContainsKey(item.Name)) // Overwrite flow
            {
                var lvi = this.Items[item.Name] as ListViewItemBase;
                this.RemoveTags(lvi.Tags);
                this.Items.RemoveByKey(item.Name);
            }

            this.Items.Add(item);
            this.AddTags(item.Tags);
        }

        public void RemoveAllItems()
        {
            // Remove custom tag columns
            for (int i = this.Columns.Count - 1; i >= FirstCustomColumnIndex; i--)
            {
                this.Columns.RemoveAt(i);
            }

            this.Items.Clear();
            this._tags.Clear();
        }

        public Exception FindItemsWithText(string regexPattern)
        {
            try
            {
                ListViewItemBase selectItem = null;
                this.BeginUpdate();
                Regex regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                foreach (ListViewItemBase lvib in this.Items)
                {
                    bool contains = lvib.Contains(regex);
                    lvib.SearchResult = contains;
                    if (selectItem == null && contains)
                    {
                        selectItem = lvib;
                    }
                }

                this.Sort();
                selectItem?.RefreshAndSelect();
                this.RefreshGroupsHeader();
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
            finally
            {
                this.EndUpdate();
            }
        }

        public void ToggleSelectedItemsToFromFavorites()
        {
            this.BeginUpdate();
            foreach (ListViewItemBase lvib in this.SelectedItems)
            {
                lvib.Favorite = !lvib.Favorite;
            }

            this.Sort();
            this.RefreshGroupsHeader();
            this.EndUpdate();
        }

        public void ExportToTsv(string filename)
        {
            StringBuilder sb = new StringBuilder();
            // Output column headers
            foreach (ColumnHeader col in this.Columns)
            {
                sb.AppendFormat("{0}\t", col.Text);
            }

            sb.Append("Status\t");
            sb.Append("Valid from time (UTC)\t");
            sb.Append("Valid until time (UTC)\t");
            sb.Append("Content Type");
            sb.AppendLine();
            // Take all items or in case of multiple selection only the selected ones
            IEnumerable<ListViewItem> items = this.SelectedItems.Count <= 1 ? this.Items.Cast<ListViewItem>() : this.SelectedItems.Cast<ListViewItem>();
            foreach (ListViewItem item in items)
            {
                sb.AppendLine(item.ToString());
            }

            File.WriteAllText(filename, sb.ToString());
        }

        private void ListViewSecrets_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            this.BeginUpdate();
            if (this.SortingColumn == e.Column) // Swap sort order
            {
                this.Sorting = this.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                this.SortingColumn = e.Column;
                this.Sorting = SortOrder.Ascending;
            }

            this.EndUpdate();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Show uxMenuStripColumns menu in case user right-click on columns header bar
            if (m.Msg == WM_CONTEXTMENU && m.WParam != this.Handle)
            {
                this.uxMenuStripColumns.Items.Clear();
                var sortedTags = from t in this._tags.Keys orderby t select t;
                foreach (string k in sortedTags)
                {
                    this.uxMenuStripColumns.Items.Add(this._tags[k]);
                }

                this.uxMenuStripColumns.Show(MousePosition);
            }
        }

        private const int WM_CONTEXTMENU = 0x7B;
        private readonly Dictionary<string, TagMenuItem> _tags;
    }
}