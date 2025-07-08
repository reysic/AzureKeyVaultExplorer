// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.Files.Tags
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Microsoft.Vault.Core;
    using Microsoft.Vault.Explorer.Model.Collections;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;

    [JsonObject]
    public class CustomTag
    {
        [JsonProperty]
        public readonly string Name;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string DefaultValue = "";

        [JsonProperty]
        public readonly Regex ValueRegex;

        [JsonProperty]
        public readonly Dictionary<string, List<TagValues>> ValueList = new Dictionary<string, List<TagValues>>();

        [JsonProperty]
        public readonly List<TagValues> CustomTagValueList = new List<TagValues>();

        [JsonConstructor]
        public CustomTag(string name, string defaultValue, string valueRegex, string[] valueList)
        {
            Guard.ArgumentNotNullOrWhitespace(name, nameof(name));
            if (name.Length > Consts.MaxTagNameLength)
            {
                throw new ArgumentOutOfRangeException("name.Length", $"Tag name '{name}' is too long, name can be up to {Consts.MaxTagNameLength} chars");
            }

            this.Name = name;
            this.DefaultValue = defaultValue;
            this.ValueRegex = new Regex(valueRegex, RegexOptions.Singleline | RegexOptions.Compiled);

            // Convert the array to a list
            if (valueList != null)
            {
                foreach (string v in valueList)
                {
                    this.CustomTagValueList.Add(new TagValues(v));
                }

                this.ValueList.Add(name, this.CustomTagValueList);
            }
        }

        public override string ToString() => this.Name;

        public TagItem ToTagItem() => new TagItem(this.Name, this.DefaultValue, this.ValueList);

        public string Verify(TagItem tagItem, bool required)
        {
            if (null == tagItem)
            {
                return required ? $"Tag {this.Name} is required\n" : "";
            }

            var m = this.ValueRegex.Match(tagItem.Value);
            return m.Success ? "" : $"Tag {this.Name} value must match the following regex: {this.ValueRegex}\n";
        }
    }

    // Used for storing a list of values for a tag
}