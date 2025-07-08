// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model
{
    using Microsoft.Vault.Explorer.Controls.Lists;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;

    public interface ISession
    {
        VaultAlias CurrentVaultAlias { get; }

        Library.Vault CurrentVault { get; }

        ListViewSecrets ListViewSecrets { get; }
    }
}