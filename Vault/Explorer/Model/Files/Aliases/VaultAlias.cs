// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.Files.Aliases
{
    using Microsoft.Vault.Core;
    using Newtonsoft.Json;

    [JsonObject]
    public class VaultAlias
    {
        [JsonProperty]
        public readonly string Alias;

        [JsonProperty]
        public readonly string[] VaultNames;

        [JsonProperty]
        public readonly string[] SecretKinds;

        public bool SecretsCollectionEnabled;

        public bool CertificatesCollectionEnabled;

        public string DomainHint;

        public string UserAlias;

        [JsonIgnore]
        public bool IsNew { get; set; }

        [JsonConstructor]
        public VaultAlias(string alias, string[] vaultNames, string[] secretKinds)
        {
            Guard.ArgumentNotNullOrEmptyString(alias, nameof(alias));
            Guard.ArgumentCollectionNotEmpty(vaultNames, nameof(vaultNames));
            Guard.ArgumentInRange(vaultNames.Length, 1, 2, nameof(vaultNames));
            this.Alias = alias;
            this.VaultNames = vaultNames;
            this.SecretKinds = secretKinds;
        }

        public override string ToString() => this.Alias;

        public override bool Equals(object obj) => obj is VaultAlias va && this.Equals(va);

        public bool Equals(VaultAlias va) => this.Alias == va?.Alias;

        public override int GetHashCode() => this.Alias?.GetHashCode() ?? 0;
    }
}