// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.Lists
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Controls.Lists.Favorites;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Library;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    /// <summary>
    ///     Base list view item which also presents itself nicely to PropertyGrid
    /// </summary>
    public abstract class ListViewItemBase : ListViewItem, ICustomTypeDescriptor
    {
        public const int SearchResultsGroup = 0;
        public const int FavoritesGroup = 1;
        public const int CertificatesGroup = 2;
        public const int KeyVaultCertificatesGroup = 3;
        public const int SecretsGroup = 4;

        public readonly ISession Session;
        public readonly int GroupIndex;
        public readonly ObjectIdentifier Identifier;
        public readonly VaultHttpsUri VaultHttpsUri;
        public readonly IDictionary<string, string> Tags;
        public readonly bool Enabled;
        public readonly DateTime? Created;
        public readonly DateTime? Updated;
        public readonly DateTime? NotBefore;
        public readonly DateTime? Expires;

        protected ListViewItemBase(ISession session, int groupIndex,
            ObjectIdentifier identifier, IDictionary<string, string> tags, bool? enabled,
            DateTime? created, DateTime? updated, DateTime? notBefore, DateTime? expires) : base(identifier.Name)
        {
            this.Session = session;
            this.GroupIndex = groupIndex;
            this.Identifier = identifier;
            this.VaultHttpsUri = new VaultHttpsUri(identifier.Identifier);
            this.Tags = tags;
            this.Enabled = enabled ?? true;
            this.Created = created;
            this.Updated = updated;
            this.NotBefore = notBefore;
            this.Expires = expires;

            this.ImageIndex = this.Enabled ? 2 * this.GroupIndex - 3 : 2 * this.GroupIndex - 2;

            this.RepopulateSubItems();

            this.ToolTipText += string.Format("Status:\t\t\t{0}\nCreation time:\t\t{1}\nLast updated time:\t{2}",
                this.Status,
                Utils.NullableDateTimeToString(created),
                Utils.NullableDateTimeToString(updated));

            this._favorite = FavoriteSecretUtil.Contains(this.Session.CurrentVaultAlias.Alias, this.Name);
            this._searchResult = false;
            this.SetGroup();
        }

        public string Status => (this.Enabled ? "Enabled" : "Disabled") + (this.Active ? ", Active" : ", Expired");

        public ListViewGroupCollection Groups => this.Session.ListViewSecrets.Groups;

        public string Id => this.VaultHttpsUri.ToString();

        public string ChangedBy => Microsoft.Vault.Library.Utils.GetChangedBy(this.Tags);

        public string Md5 => Microsoft.Vault.Library.Utils.GetMd5(this.Tags);

        public string Link => $"{Globals.ActivationUrl}?{this.VaultHttpsUri.VaultLink}";

        public bool AboutToExpire => DateTime.UtcNow + Settings.Default.AboutToExpireWarningPeriod <= (this.Expires ?? DateTime.MaxValue);

        /// <summary>
        ///     True only if current time is within the below range, or range is NULL
        ///     [NotBefore] Valid from time (UTC)
        ///     [Expires] Valid until time (UTC)
        /// </summary>
        public bool Active => DateTime.UtcNow >= (this.NotBefore ?? DateTime.MinValue) && DateTime.UtcNow <= (this.Expires ?? DateTime.MaxValue);

        private static readonly string[] GroupIndexToName = new[] { "s", "f", "certificate", "key vault certificate", "secret" };
        public string Kind => GroupIndexToName[this.GroupIndex];

        public void RepopulateSubItems()
        {
            this.SubItems.Clear();
            this.SubItems[0].Name = this.SubItems[0].Text = this.Identifier.Name;
            this.SubItems.Add(new ListViewSubItem(this, Utils.NullableDateTimeToString(this.Updated)) { Tag = this.Updated }); // Add Tag so ListViewItemSorter will sort DateTime correctly
            this.SubItems.Add(this.ChangedBy);
            this.SubItems.Add(new ListViewSubItem(this, Utils.ExpirationToString(this.Expires)) { Tag = this.Expires }); // Add Tag so ListViewItemSorter will sort TimeSpan correctly
            // Add tag value for all the custom columns
            for (int i = ListViewSecrets.FirstCustomColumnIndex; i < this.Session.ListViewSecrets.Columns.Count; i++)
            {
                string key = this.Session.ListViewSecrets.Columns[i].Name;
                this.SubItems.Add(null == this.Tags || this.Tags.Count == 0 || !this.Tags.ContainsKey(key) ? "" : string.IsNullOrWhiteSpace(this.Tags[key]) ? "(none)" : this.Tags[key]);
            }

            this.ForeColor = this.AboutToExpire ? this.ForeColor : Settings.Default.AboutToExpireItemColor;
            this.ForeColor = this.Active ? this.ForeColor : Settings.Default.ExpiredItemColor;
            this.ForeColor = this.Enabled ? this.ForeColor : Settings.Default.DisabledItemColor;
        }

        public void RefreshAndSelect()
        {
            this.Session.ListViewSecrets.MultiSelect = false;
            this.EnsureVisible();
            this.Focused = this.Selected = false;
            this.Focused = this.Selected = true;
            this.Session.ListViewSecrets.MultiSelect = true;
        }

        private void SetGroup()
        {
            this.Group = this._searchResult ? this.Groups[SearchResultsGroup] : this._favorite ? this.Groups[FavoritesGroup] : this.Groups[this.GroupIndex];
        }

        private bool _searchResult;

        public bool SearchResult
        {
            get { return this._searchResult; }
            set
            {
                this._searchResult = value;
                this.SetGroup();
            }
        }

        private bool _favorite;

        public bool Favorite
        {
            get { return this._favorite; }
            set
            {
                this._favorite = value;
                this.SetGroup();
                if (this._favorite)
                {
                    FavoriteSecretUtil.Add(this.Session.CurrentVaultAlias.Alias, this.Name);
                }
                else
                {
                    FavoriteSecretUtil.Remove(this.Session.CurrentVaultAlias.Alias, this.Name);
                }
            }
        }

        public bool Contains(Regex regexPattern)
        {
            if (string.IsNullOrWhiteSpace(regexPattern.ToString()))
            {
                return false;
            }

            foreach (ReadOnlyPropertyDescriptor ropd in this.GetProperties(null))
            {
                if (regexPattern.Match($"{ropd.Name}={ropd.Value}").Success)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool VerifyDuplication(ISession session, string oldName, PropertyObject soNew)
        {
            string newMd5 = soNew.Md5;

            // Check if we already have *another* secret with the same name
            if (oldName != soNew.Name && session.ListViewSecrets.Items.ContainsKey(soNew.Name) &&
                MessageBox.Show($"Are you sure you want to replace existing item '{soNew.Name}' with new value?", Globals.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return false;
            }

            // Detect dups by Md5
            var sameSecretsList = from slvi in session.ListViewSecrets.Items.Cast<ListViewItemBase>() where slvi.Md5 == newMd5 && slvi.Name != oldName && slvi.Name != soNew.Name select slvi.Name;
            if (sameSecretsList.Count() > 0 &&
                MessageBox.Show($"There are {sameSecretsList.Count()} other item(s) in the vault which has the same Md5: {newMd5}.\nHere the name(s) of the other items:\n{string.Join(", ", sameSecretsList)}\nAre you sure you want to add or update item {soNew.Name} and have a duplication of secrets?", Globals.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return false;
            }

            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (ListViewSubItem subItem in this.SubItems)
            {
                sb.AppendFormat("{0}\t", subItem.Text);
            }

            sb.AppendFormat("{0}\t", this.Status);
            sb.AppendFormat("{0}\t", Utils.NullableDateTimeToString(this.NotBefore));
            sb.AppendFormat("{0}\t", Utils.NullableDateTimeToString(this.Expires));
            sb.AppendFormat("{0}", ContentTypeEnumConverter.GetDescription(this.GetContentType()));

            return sb.ToString();
        }

        protected abstract IEnumerable<PropertyDescriptor> GetCustomProperties();

        public abstract ContentType GetContentType();

        public abstract Task<PropertyObject> GetAsync(CancellationToken cancellationToken);

        public abstract Task<ListViewItemBase> ToggleAsync(CancellationToken cancellationToken);

        public abstract Task<ListViewItemBase> ResetExpirationAsync(CancellationToken cancellationToken);

        public abstract Task<ListViewItemBase> DeleteAsync(CancellationToken cancellationToken);

        public abstract Task<IEnumerable<object>> GetVersionsAsync(CancellationToken cancellationToken);

        public abstract Form GetEditDialog(string name, IEnumerable<object> versions);

        public abstract Task<ListViewItemBase> UpdateAsync(object originalObject, PropertyObject newObject, CancellationToken cancellationToken);

        #region ICustomTypeDescriptor interface to show properties in PropertyGrid

        public string GetComponentName() => TypeDescriptor.GetComponentName(this, true);

        public EventDescriptor GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);

        public string GetClassName() => TypeDescriptor.GetClassName(this, true);

        public EventDescriptorCollection GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);

        public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);

        public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this, true);

        public object GetPropertyOwner(PropertyDescriptor pd) => this.Id;

        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);

        public object GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);

        public PropertyDescriptor GetDefaultProperty() => null;

        public PropertyDescriptorCollection GetProperties() => this.GetProperties(new Attribute[0]);

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>
            {
                new ReadOnlyPropertyDescriptor("Name", this.Name),
                new ReadOnlyPropertyDescriptor("Link", this.Link),
                new ReadOnlyPropertyDescriptor("Identifier", this.Id),
                new ReadOnlyPropertyDescriptor("Creation time", Utils.NullableDateTimeToString(this.Created)),
                new ReadOnlyPropertyDescriptor("Last updated time", Utils.NullableDateTimeToString(this.Updated)),
                new ReadOnlyPropertyDescriptor("Enabled", this.Enabled),
                new ReadOnlyPropertyDescriptor("Valid from time (UTC)", this.NotBefore),
                new ReadOnlyPropertyDescriptor("Valid until time (UTC)", this.Expires),
            };
            properties.AddRange(this.GetCustomProperties());
            if (this.Tags != null)
            {
                foreach (var kvp in this.Tags)
                {
                    properties.Add(new ReadOnlyPropertyDescriptor(kvp.Key, kvp.Value));
                }
            }

            return new PropertyDescriptorCollection(properties.ToArray());
        }

        #endregion
    }
}