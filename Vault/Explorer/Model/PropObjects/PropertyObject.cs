// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.PropObjects
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Drawing.Design;
    using System.IO;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Vault.Explorer.Controls;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Model.Collections;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Library;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    /// <summary>
    ///     Base class to edit an object via PropertyGrid
    /// </summary>
    [DefaultProperty("Tags")]
    public abstract class PropertyObject : INotifyPropertyChanged
    {
        protected void NotifyPropertyChanged(string info) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));

        protected ContentType _contentType;
        protected string _version;

        public ContentType GetContentType() => this._contentType;

        public event PropertyChangedEventHandler PropertyChanged;

        public readonly ObjectIdentifier Identifier;

        [DisplayName("Name")]
        [Browsable(false)]
        public string Name { get; set; }

        [Category("General")]
        [DisplayName("Custom Tags")]
        public ObservableTagItemsCollection Tags { get; set; }

        private bool? _enabled;

        [Category("General")]
        [DisplayName("Enabled")]
        public bool? Enabled
        {
            get { return this._enabled; }
            set
            {
                this._enabled = value;
                this.NotifyPropertyChanged(nameof(this.Enabled));
            }
        }

        private DateTime? _notBefore;

        [Category("General")]
        [DisplayName("Valid from time (UTC)")]
        [Editor(typeof(NullableDateTimePickerEditor), typeof(UITypeEditor))]
        public DateTime? NotBefore
        {
            get { return this._notBefore; }
            set
            {
                this._notBefore = value;
                this.NotifyPropertyChanged(nameof(this.NotBefore));
            }
        }

        private DateTime? _expires;

        [Category("General")]
        [DisplayName("Valid until time (UTC)")]
        [Editor(typeof(NullableDateTimePickerEditor), typeof(UITypeEditor))]
        public DateTime? Expires
        {
            get { return this._expires; }
            set
            {
                this._expires = value;
                this.NotifyPropertyChanged(nameof(this.Expires));
            }
        }

        /// <summary>
        ///     Human readable value of the secret
        /// </summary>
        protected string _value;

        [DisplayName("Value")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        [Browsable(false)]
        public string Value
        {
            get { return this._value; }
            set
            {
                if (this._value != value)
                {
                    this._value = value;
                    this.NotifyPropertyChanged(nameof(this.Value));
                }
            }
        }

        /// <summary>
        ///     Raw value to store in the vault
        /// </summary>
        [Browsable(false)]
        public string RawValue => this._contentType.ToRawValue(this._value);

        /// <summary>
        ///     Md5 of the raw value
        /// </summary>
        [Browsable(false)]
        public string Md5 => Microsoft.Vault.Library.Utils.CalculateMd5(this.RawValue);

        /// <summary>
        ///     Current SecretKind for this secret object
        ///     Note: NotifyPropertyChanged is NOT called upon set
        /// </summary>
        [Browsable(false)]
        public SecretKind SecretKind { get; set; }

        [Browsable(false)]
        public bool IsNameValid => this.Name == null ? false : this.SecretKind.NameRegex.IsMatch(this.Name);

        [Browsable(false)]
        public bool IsValueValid => this.Value == null ? false : this.SecretKind.ValueRegex.IsMatch(this.Value);

        [Browsable(false)]
        public bool IsExpirationValid => (this.NotBefore ?? DateTime.MinValue) < (this.Expires ?? DateTime.MaxValue)
                                         && (this.Expires ?? DateTime.MaxValue) <= (this.SecretKind.MaxExpiration == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.UtcNow + this.SecretKind.MaxExpiration);

        protected PropertyObject(ObjectIdentifier identifier, IDictionary<string, string> tags,
            bool? enabled, DateTime? expires, DateTime? notBefore,
            PropertyChangedEventHandler propertyChanged)
        {
            this.Identifier = identifier;
            this.Name = identifier?.Name;

            this.Tags = new ObservableTagItemsCollection();
            if (null != tags)
            {
                foreach (var kvp in tags)
                {
                    this.Tags.Add(new TagItem(kvp));
                }
            }

            this.Tags.SetPropertyChangedEventHandler(propertyChanged);

            this._enabled = enabled;
            this._expires = expires;
            this._notBefore = notBefore;

            this.SecretKind = new SecretKind(); // Default - Custom secret kind

            this.PropertyChanged += propertyChanged;
        }

        public abstract string GetKeyVaultFileExtension();

        public virtual DataObject GetClipboardValue()
        {
            var dataObj = new DataObject("Preferred DropEffect", DragDropEffects.Move); // "Cut" file to clipboard
            if (this._contentType.IsCertificate()) // Common logic for .cer and .pfx
            {
                var tempPath = Path.Combine(Path.GetTempPath(), this.Name + this._contentType.ToExtension());
                this.SaveToFile(tempPath);
                var sc = new StringCollection();
                sc.Add(tempPath);
                dataObj.SetFileDropList(sc);
            }

            return dataObj;
        }

        public abstract void SaveToFile(string fullName);

        protected abstract IEnumerable<TagItem> GetValueBasedCustomTags();

        public abstract void PopulateCustomTags();

        public abstract void AddOrUpdateSecretKind(SecretKind sk);

        public abstract string AreCustomTagsValid();

        public abstract void PopulateExpiration();

        public Dictionary<string, string> ToTagsDictionary()
        {
            var result = new Dictionary<string, string>();
            // Add all user and custom tags
            foreach (var tagItem in this.Tags)
            {
                result[tagItem.Name] = tagItem.Value;
            }

            // Add all custom tags which are based on the secret value
            foreach (var tagItem in this.GetValueBasedCustomTags())
            {
                result[tagItem.Name] = tagItem.Value;
            }

            // Note: Md5 and ChangeBy tags are taken care in the Microsoft.Vault.Library
            return result;
        }

        public string GetFileName() => this.Name + this._contentType.ToExtension();

        public void CopyToClipboard(bool showToast)
        {
            var dataObj = this.GetClipboardValue();
            if (null != dataObj)
            {
                Clipboard.SetDataObject(dataObj, true);
                Utils.ClearCliboard(Settings.Default.CopyToClipboardTimeToLive, Microsoft.Vault.Library.Utils.CalculateMd5(dataObj.GetText()));
                if (showToast)
                {
                    Utils.ShowToast($"{(this._contentType.IsCertificate() ? "Certificate" : "Secret")} {this.Name} copied to clipboard");
                }
            }
        }

        public string GetLinkAsInternetShortcut() => $"[InternetShortcut]\nURL={new VaultHttpsUri(this.Identifier.Identifier).VaultLink}";
    }
}