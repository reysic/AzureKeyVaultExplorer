namespace Microsoft.Vault.Explorer.Controls.Lists
{
    using System;
    using System.Collections;
    using System.Windows.Forms;

    /// <summary>
    ///     Simple ListViewItemSorter to sort by two columns (Strikeout and Column index)
    /// </summary>
    public class ListViewSecretsSorter : IComparer
    {
        private readonly ListViewSecrets _control;

        public ListViewSecretsSorter(ListViewSecrets control)
        {
            this._control = control;
        }

        public int Compare(object x, object y)
        {
            ListViewItemBase sx = (ListViewItemBase)x;
            ListViewItemBase sy = (ListViewItemBase)y;

            int col = Math.Min(this._control.SortingColumn, this._control.Columns.Count - 1);

            ListViewItem.ListViewSubItem a = sx.SubItems[col];
            ListViewItem.ListViewSubItem b = sy.SubItems[col];

            int c = 0;
            if (a.Tag != null && b.Tag != null)
            {
                // Compare DateTime
                if (a.Tag is DateTime? && b.Tag is DateTime? && (a.Tag as DateTime?).HasValue && (b.Tag as DateTime?).HasValue)
                {
                    var adt = (a.Tag as DateTime?).Value;
                    var bdt = (b.Tag as DateTime?).Value;
                    c = DateTime.Compare(adt, bdt);
                }

                // Compare TimeSpan
                if (a.Tag is TimeSpan? && b.Tag is TimeSpan? && (a.Tag as TimeSpan?).HasValue && (b.Tag as TimeSpan?).HasValue)
                {
                    var ats = (a.Tag as TimeSpan?).Value;
                    var bts = (b.Tag as TimeSpan?).Value;
                    c = TimeSpan.Compare(ats, bts);
                }
            }
            else
            {
                c = string.Compare(a.Text, b.Text);
            }

            return this._control.Sorting == SortOrder.Descending ? -c : c;
        }
    }
}