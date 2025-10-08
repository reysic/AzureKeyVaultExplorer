namespace Microsoft.Vault.Explorer.Model.Files.Tags
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Vault.Core;
    using Newtonsoft.Json;

    [JsonDictionary]
    public class CustomTags : Dictionary<string, CustomTag>
    {
        public CustomTags()
        {
        }

        [JsonConstructor]
        public CustomTags(IDictionary<string, CustomTag> customTags) : base(customTags, StringComparer.CurrentCultureIgnoreCase)
        {
            foreach (string customTagKey in this.Keys)
            {
                Guard.ArgumentNotNullOrWhitespace(customTagKey, nameof(customTagKey));
            }
        }
    }
}