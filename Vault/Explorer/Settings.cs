// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Configuration;
    using System.Drawing;
    using System.Drawing.Design;
    using System.Linq;
    using System.Windows.Forms.Design;
    using Microsoft.Vault.Explorer.Controls.Lists.Favorites;
    using Newtonsoft.Json;

    public class Settings : ApplicationSettingsBase
    {
        private static readonly Settings defaultInstance = (Settings)Synchronized(new Settings());
        private readonly FavoriteSecretsDictionary _favoriteSecretsDictionary;

        public static Settings Default
        {
            get { return defaultInstance; }
        }

        public Settings()
        {
            this._favoriteSecretsDictionary = JsonConvert.DeserializeObject<FavoriteSecretsDictionary>(this.FavoriteSecretsJson);
        }

        [UserScopedSetting]
        [DefaultSettingValue("00:00:30")]
        [DisplayName("Clear secret from clipboard after")]
        [Description("Interval for secret to stay in the clipboard once copied to the clipboard.")]
        [Category("General")]
        public TimeSpan CopyToClipboardTimeToLive
        {
            get { return (TimeSpan)this[nameof(this.CopyToClipboardTimeToLive)]; }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.CopyToClipboardTimeToLive));
                }

                this[nameof(this.CopyToClipboardTimeToLive)] = value;
            }
        }

        [UserScopedSetting]
        [DefaultSettingValue("14.00:00:00")]
        [DisplayName("About to expire warning period")]
        [Description("Warning interval to use for items that are close to their expiration date.")]
        [Category("General")]
        public TimeSpan AboutToExpireWarningPeriod
        {
            get { return (TimeSpan)this[nameof(this.AboutToExpireWarningPeriod)]; }
            set { this[nameof(this.AboutToExpireWarningPeriod)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("Orange")]
        [DisplayName("About to expire item color")]
        [Description("Color to use for items that are close to their expiration date.")]
        [Category("General")]
        public Color AboutToExpireItemColor
        {
            get { return (Color)this[nameof(this.AboutToExpireItemColor)]; }
            set { this[nameof(this.AboutToExpireItemColor)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("Red")]
        [DisplayName("Expired item color")]
        [Description("Color to use for expired or not yet active item.")]
        [Category("General")]
        public Color ExpiredItemColor
        {
            get { return (Color)this[nameof(this.ExpiredItemColor)]; }
            set { this[nameof(this.ExpiredItemColor)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("GrayText")]
        [DisplayName("Disabled item color")]
        [Description("Color to use for disabled item.")]
        [Category("General")]
        public Color DisabledItemColor
        {
            get { return (Color)this[nameof(this.DisabledItemColor)]; }
            set { this[nameof(this.DisabledItemColor)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@".\")]
        [DisplayName("Root location")]
        [Description("Relative or absolute path to root folder where .json files are located.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        public string JsonConfigurationFilesRoot
        {
            get { return (string)this[nameof(this.JsonConfigurationFilesRoot)]; }
            set { this[nameof(this.JsonConfigurationFilesRoot)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"Vaults.json")]
        [DisplayName("Vaults file name")]
        [Description("Relative or absolute path to .json file with vaults definitions and access.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string VaultsJsonFileLocation
        {
            get { return (string)this[nameof(this.VaultsJsonFileLocation)]; }
            set { this[nameof(this.VaultsJsonFileLocation)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"VaultAliases.json")]
        [DisplayName("Vault aliases file name")]
        [Description("Relative or absolute path to .json file with vault aliases.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string VaultAliasesJsonFileLocation
        {
            get { return (string)this[nameof(this.VaultAliasesJsonFileLocation)]; }
            set { this[nameof(this.VaultAliasesJsonFileLocation)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"SecretKinds.json")]
        [DisplayName("Secret kinds file name")]
        [Description("Relative or absolute path to .json file with secret kinds definitions.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string SecretKindsJsonFileLocation
        {
            get { return (string)this[nameof(this.SecretKindsJsonFileLocation)]; }
            set { this[nameof(this.SecretKindsJsonFileLocation)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"CustomTags.json")]
        [DisplayName("Custom tags file name")]
        [Description("Relative or absolute path to .json file with custom tags definitions.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string CustomTagsJsonFileLocation
        {
            get { return (string)this[nameof(this.CustomTagsJsonFileLocation)]; }
            set { this[nameof(this.CustomTagsJsonFileLocation)] = value; }
        }


        [UserScopedSetting]
        [DefaultSettingValue("[ENTER_YOUR_USER_ACCOUNT_HERE]")]
        [DisplayName("User Account Names")]
        [Description("Multi-line string of user account names to use in the subscriptions manager dialog.")]
        [Category("Subscriptions dialog")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string UserAccountNames
        {
            get { return (string)this[nameof(this.UserAccountNames)]; }
            set { this[nameof(this.UserAccountNames)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        [Browsable(false)]
        public bool UpgradeRequired
        {
            get { return (bool)this[nameof(this.UpgradeRequired)]; }
            set { this[nameof(this.UpgradeRequired)] = value; }
        }

        [Browsable(false)]
        public IEnumerable<string> UserAccountNamesList
        {
            get
            {
                // Set default if empty
                if (string.IsNullOrEmpty(this.UserAccountNames))
                {
                    this.UserAccountNames = $"{Environment.UserName}@microsoft.com";
                }

                return from s in this.UserAccountNames.Split('\n') where !string.IsNullOrWhiteSpace(s) select s.Trim();
            }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"{}")]
        [Browsable(false)]
        public string FavoriteSecretsJson
        {
            get { return (string)this[nameof(this.FavoriteSecretsJson)]; }
        }

        [Browsable(false)]
        public FavoriteSecretsDictionary FavoriteSecretsDictionary
        {
            get { return this._favoriteSecretsDictionary; }
        }

        public override void Save()
        {
            // new lines and spaces so user.config will look pretty
            this[nameof(this.FavoriteSecretsJson)] = "\n" + JsonConvert.SerializeObject(this._favoriteSecretsDictionary, Formatting.Indented) + "\n                ";
            base.Save();
        }

        // Adds and saves new user alias in app settings.
        public void AddUserAccountName(string userAccountName)
        {
            if (!this.UserAccountNames.Contains(userAccountName))
            {
                this[nameof(this.UserAccountNames)] = this.UserAccountNames + "\n" + userAccountName;
                base.Save();
            }
        }
    }
}