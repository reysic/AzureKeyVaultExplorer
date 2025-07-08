// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.MenuItems
{
    using System;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    [JsonObject]
    public class SecretKind : ToolStripMenuItem
    {
        [JsonProperty]
        public readonly string Alias;

        [JsonProperty]
        public readonly string Description;

        [JsonProperty]
        public readonly Regex NameRegex;

        [JsonProperty]
        public readonly Regex ValueRegex;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string ValueTemplate;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string CertificateFormat;

        [JsonIgnore]
        public bool IsCertificate => !string.IsNullOrEmpty(this.CertificateFormat);

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string[] RequiredCustomTags;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string[] OptionalCustomTags;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly TimeSpan DefaultExpiration;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly TimeSpan MaxExpiration;

        public SecretKind() : base("Custom")
        {
            this.Alias = "Custom";
            this.ToolTipText = this.Description = "The name must be a string 1-127 characters in length containing only 0-9, a-z, A-Z, and -.";
            this.NameRegex = Consts.ValidSecretNameRegex;
            this.ValueRegex = new Regex("^.{0,1048576}$", RegexOptions.Singleline | RegexOptions.Compiled);
            this.ValueTemplate = "";
            this.CertificateFormat = null;
            this.RequiredCustomTags = new string[0];
            this.OptionalCustomTags = new string[0];
            this.MaxExpiration = TimeSpan.MaxValue;
        }

        public SecretKind(string alias) : base(alias)
        {
            this.Alias = alias;
            this.ToolTipText = this.Description = "The name must be a string 1-127 characters in length containing only 0-9, a-z, A-Z, and -.";
            this.NameRegex = Consts.ValidSecretNameRegex;
            this.ValueRegex = new Regex("^.{0,1048576}$", RegexOptions.Singleline | RegexOptions.Compiled);
            this.ValueTemplate = "";
            this.CertificateFormat = null;
            this.RequiredCustomTags = new string[0];
            this.OptionalCustomTags = new string[0];
            this.MaxExpiration = TimeSpan.MaxValue;
        }

        [JsonConstructor]
        public SecretKind(string alias, string description, string nameRegex, string valueRegex, string valueTemplate,
            string certificateFormat, string[] requiredCustomTags, string[] optionalCustomTags,
            TimeSpan defaultExpiration, TimeSpan maxExpiration) : base(alias)
        {
            this.Alias = alias;
            this.ToolTipText = this.Description = description;
            this.NameRegex = new Regex(nameRegex, RegexOptions.Singleline | RegexOptions.Compiled);
            this.ValueRegex = new Regex(valueRegex, RegexOptions.Singleline | RegexOptions.Compiled);
            this.ValueTemplate = valueTemplate;
            this.CertificateFormat = certificateFormat;
            this.RequiredCustomTags = requiredCustomTags ?? new string[0];
            this.OptionalCustomTags = optionalCustomTags ?? new string[0];
            this.DefaultExpiration = defaultExpiration;
            this.MaxExpiration = default(TimeSpan) == maxExpiration ? TimeSpan.MaxValue : maxExpiration;
            if (this.DefaultExpiration > this.MaxExpiration)
            {
                throw new ArgumentOutOfRangeException("DefaultExpiration or MaxExpiration", $"DefaultExpiration value must be less than MaxExpiration in secret kind {alias}");
            }

            if (this.RequiredCustomTags.Length + this.OptionalCustomTags.Length > Consts.MaxNumberOfTags)
            {
                throw new ArgumentOutOfRangeException("Total CustomTags.Length", $"Too many custom tags for secret kind {alias}, maximum number of tags for secret is only {Consts.MaxNumberOfTags}");
            }
        }

        public override string ToString() => this.Text + " secret name" + Utils.DropDownSuffix;
    }
}