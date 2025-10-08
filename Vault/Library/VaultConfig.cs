// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    [JsonDictionary]
    public class VaultsConfig : Dictionary<string, VaultAccessType>
    {
        [JsonConstructor]
        public VaultsConfig(IDictionary<string, VaultAccessType> vaults) : base(vaults, StringComparer.CurrentCultureIgnoreCase)
        {
            foreach (string vaultName in this.Keys)
            {
                Utils.GuardVaultName(vaultName);
            }
        }
    }
}