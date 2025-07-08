// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.Lists.Favorites
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;

    [JsonDictionary]
    public class FavoriteSecrets : Dictionary<string, FavoriteSecret>
    {
        public FavoriteSecrets()
        {
        }

        [JsonConstructor]
        public FavoriteSecrets(IDictionary<string, FavoriteSecret> dictionary) : base(dictionary, StringComparer.CurrentCultureIgnoreCase)
        {
            foreach (string secretName in this.Keys)
            {
                if (false == Consts.ValidSecretNameRegex.IsMatch(secretName))
                {
                    throw new ArgumentException($"Invalid secret name {secretName}");
                }
            }
        }
    }
}