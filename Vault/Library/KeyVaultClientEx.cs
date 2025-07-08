// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using Microsoft.Azure.KeyVault;

    /// <summary>
    ///     Simple wrapper around KeyVaultClient
    /// </summary>
    internal class KeyVaultClientEx : KeyVaultClient
    {
        public readonly string VaultName;
        public readonly string VaultUri;

        public KeyVaultClientEx(string vaultName, AuthenticationCallback authenticationCallback) : base(authenticationCallback)
        {
            Utils.GuardVaultName(vaultName);
            this.VaultName = vaultName;
            this.VaultUri = string.Format(Consts.AzureVaultUriFormat, this.VaultName);
        }

        private string ToIdentifier(string endpoint, string name, string version) => $"{this.VaultUri}/{endpoint}/{name}" + (string.IsNullOrEmpty(version) ? "" : $"/{version}");

        public string ToSecretIdentifier(string secretName, string version = null) => this.ToIdentifier(Consts.SecretsEndpoint, secretName, version);

        public string ToCertificateIdentifier(string certificateName, string version = null) => this.ToIdentifier(Consts.CertificatesEndpoint, certificateName, version);

        public override string ToString() => this.VaultUri;
    }
}