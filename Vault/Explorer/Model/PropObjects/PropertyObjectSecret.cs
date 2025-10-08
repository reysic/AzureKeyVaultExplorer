// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.PropObjects
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Model.Collections;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Secrets;
    using Microsoft.Vault.Explorer.Model.Files.Tags;
    using Microsoft.Vault.Library;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    /// <summary>
    ///     Secret object to edit via PropertyGrid
    /// </summary>
    public class PropertyObjectSecret : PropertyObject
    {
        /// <summary>
        ///     Original secret
        /// </summary>
        private readonly SecretBundle _secret;

        private readonly CustomTags _customTags;

        [Category("General")]
        [DisplayName("Content Type")]
        [TypeConverter(typeof(ContentTypeEnumConverter))]
        public ContentType ContentType
        {
            get { return this._contentType; }
            set
            {
                this._contentType = value;
                this.NotifyPropertyChanged(nameof(this.ContentType));
            }
        }

        [Category("X")]
        [DisplayName(@"Version")]
        [Display(Order = 99)]
        public string Version
        {
            get { return this._version; }
        }

        public PropertyObjectSecret(SecretBundle secret, PropertyChangedEventHandler propertyChanged) :
            base(secret.SecretIdentifier, secret.Tags, secret.Attributes.Enabled, secret.Attributes.Expires, secret.Attributes.NotBefore, propertyChanged)
        {
            this._secret = secret;
            this._version = secret.SecretIdentifier?.Version;
            this._contentType = ContentTypeEnumConverter.GetValue(secret.ContentType);
            this._value = this._contentType.FromRawValue(secret.Value);
            this._customTags = Utils.LoadFromJsonFile<CustomTags>(Settings.Default.CustomTagsJsonFileLocation, isOptional: true);
        }

        protected override IEnumerable<TagItem> GetValueBasedCustomTags()
        {
            // Add tags based on all named groups in the value regex
            Match m = this.SecretKind.ValueRegex.Match(this.Value);
            if (m.Success)
            {
                for (int i = 0; i < m.Groups.Count; i++)
                {
                    string groupName = this.SecretKind.ValueRegex.GroupNameFromNumber(i);
                    if (groupName == i.ToString())
                    {
                        continue; // Skip unnamed groups
                    }

                    yield return new TagItem(groupName, m.Groups[i].Value);
                }
            }
        }

        public override void PopulateCustomTags()
        {
            if (null == this._customTags || this._customTags.Count == 0)
            {
                return;
            }

            // Add RequiredCustomTags and OptionalCustomTags
            foreach (var tagId in this.SecretKind.RequiredCustomTags.Concat(this.SecretKind.OptionalCustomTags))
            {
                if (false == this._customTags.ContainsKey(tagId))
                {
                    continue;
                }

                this.Tags.AddOrKeep(this._customTags[tagId].ToTagItem());
            }
        }

        // This method updates the SecretKind tag in Custom Tags
        public override void AddOrUpdateSecretKind(SecretKind sk)
        {
            TagItem newTag = new TagItem(Consts.SecretKindKey, sk.Alias);
            TagItem oldTag = this.Tags.GetOrNull(newTag);

            // Don't add the SecretKind to a secret that doesn't have any custom tags
            if (null == this._customTags)
            {
                return;
            }

            // Don't add the SecretKind to a secret that's defaulted to Custom
            if (sk.Alias == "Custom" && !this.Tags.Contains(newTag))
            {
                return;
            }

            // Don't add the SecretKind to a secret that is defaulted to Custom and doesn't have any custom tags.
            if (oldTag == null && newTag.Value == "Custom")
            {
                return;
            }

            if (oldTag == null) // Add the SecretKind tag
            {
                this.Tags.AddOrReplace(newTag);
            }
            else if (oldTag.Value != newTag.Value) // Update the SecretKind tag
            {
                this.Tags.AddOrReplace(newTag);
            }
            else // Leave the SecretKind tag alone
            {
                this.Tags.AddOrReplace(oldTag);
            }
        }

        public override string AreCustomTagsValid()
        {
            if (null == this._customTags || this._customTags.Count == 0)
            {
                return "";
            }

            StringBuilder result = new StringBuilder();
            // Verify RequiredCustomTags
            foreach (var tagId in this.SecretKind.RequiredCustomTags)
            {
                if (false == this._customTags.ContainsKey(tagId))
                {
                    continue;
                }

                var ct = this._customTags[tagId];
                result.Append(ct.Verify(this.Tags.GetOrNull(ct.ToTagItem()), true));
            }

            // Verify OptionalCustomTags
            foreach (var tagId in this.SecretKind.OptionalCustomTags)
            {
                if (false == this._customTags.ContainsKey(tagId))
                {
                    continue;
                }

                var ct = this._customTags[tagId];
                result.Append(ct.Verify(this.Tags.GetOrNull(ct.ToTagItem()), false));
            }

            return result.ToString();
        }

        public override void PopulateExpiration()
        {
            // Set item expiration in case DefaultExpiration is not zero
            this.Expires = default(TimeSpan) == this.SecretKind.DefaultExpiration ? null : DateTime.UtcNow.Add(this.SecretKind.DefaultExpiration);
        }

        public SecretAttributes ToSecretAttributes() => new SecretAttributes
        {
            Enabled = this.Enabled,
            Expires = this.Expires,
            NotBefore = this.NotBefore,
        };

        public override string GetKeyVaultFileExtension() => ContentType.KeyVaultSecret.ToExtension();

        public override DataObject GetClipboardValue()
        {
            var dataObj = base.GetClipboardValue();
            // We use SetData() and not SetText() to support correctly empty string "" as a value
            dataObj.SetData(DataFormats.UnicodeText, this.ContentType.IsCertificate() ? CertificateValueObject.FromValue(this.Value)?.Password : this.Value);
            return dataObj;
        }

        public override void SaveToFile(string fullName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullName));
            switch (ContentTypeUtils.FromExtension(Path.GetExtension(fullName)))
            {
                case ContentType.KeyVaultSecret: // Serialize the entire secret as encrypted JSON for current user
                    File.WriteAllText(fullName, new KeyVaultSecretFile(this._secret).Serialize());
                    break;
                case ContentType.KeyVaultCertificate:
                    throw new InvalidOperationException("One can't save key vault secret as key vault certificate");
                case ContentType.KeyVaultLink:
                    File.WriteAllText(fullName, this.GetLinkAsInternetShortcut());
                    break;
                case ContentType.Certificate:
                    File.WriteAllBytes(fullName, CertificateValueObject.FromValue(this.Value).Certificate.Export(X509ContentType.Cert));
                    break;
                case ContentType.Pkcs12:
                    File.WriteAllBytes(fullName, Convert.FromBase64String(CertificateValueObject.FromValue(this.Value).Data));
                    break;
                default:
                    File.WriteAllText(fullName, this.Value);
                    break;
            }
        }
    }
}