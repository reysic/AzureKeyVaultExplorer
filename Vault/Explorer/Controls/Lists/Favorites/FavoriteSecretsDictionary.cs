namespace Microsoft.Vault.Explorer.Controls.Lists.Favorites
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Vault.Core;
    using Newtonsoft.Json;

    [JsonDictionary]
    public class FavoriteSecretsDictionary : Dictionary<string, FavoriteSecrets>
    {
        [JsonConstructor]
        public FavoriteSecretsDictionary(IDictionary<string, FavoriteSecrets> dictionary) : base(dictionary, StringComparer.CurrentCultureIgnoreCase)
        {
            foreach (string vaultAlias in this.Keys)
            {
                Guard.ArgumentNotNullOrWhitespace(vaultAlias, nameof(vaultAlias));
            }
        }
    }
}