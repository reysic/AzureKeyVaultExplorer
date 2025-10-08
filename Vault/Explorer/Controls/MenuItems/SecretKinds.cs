namespace Microsoft.Vault.Explorer.Controls.MenuItems
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Vault.Core;
    using Newtonsoft.Json;

    [JsonDictionary]
    public class SecretKinds : Dictionary<string, SecretKind>
    {
        public SecretKinds()
        {
        }

        [JsonConstructor]
        public SecretKinds(IDictionary<string, SecretKind> secretKinds) : base(secretKinds, StringComparer.CurrentCultureIgnoreCase)
        {
            foreach (string secretKindName in this.Keys)
            {
                Guard.ArgumentNotNullOrWhitespace(secretKindName, nameof(secretKindName));
            }
        }
    }
}