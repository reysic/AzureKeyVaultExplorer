namespace Microsoft.Vault.Explorer.Model.Collections
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using Microsoft.Vault.Core;
    using Microsoft.Vault.Explorer.Model.Files.Tags;
    using Microsoft.Vault.Library;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    public class TagItem
    {
        private string _name;
        private string _value;
        private List<TagValueListItems> _valueList = new List<TagValueListItems>();
        private static Dictionary<string, List<TagValueListItems>> _valueListDictionary = new Dictionary<string, List<TagValueListItems>>();
        private bool _isList;

        [Category("Tag")]
        public string Name
        {
            get { return this._name; }
            set
            {
                Guard.ArgumentNotNullOrEmptyString(value, nameof(value));
                if (value.Length > Consts.MaxTagNameLength)
                {
                    throw new ArgumentOutOfRangeException("Name.Length", $"Tag name '{value}' is too long, name can be up to {Consts.MaxTagNameLength} chars");
                }

                this._name = value;
            }
        }

        [Category("Value")]
        [TypeConverter(typeof(TagValueListItemConverter))]
        public string Value
        {
            get { return this._value; }
            set
            {
                Guard.ArgumentNotNull(value, nameof(value));
                if (value.Length > Consts.MaxTagValueLength)
                {
                    throw new ArgumentOutOfRangeException("Value.Length", $"Tag value '{value}' is too long, value can be up to {Consts.MaxTagValueLength} chars");
                }

                this._value = value;
            }
        }


        [Category("ValueList")]
        [Browsable(false)]
        public List<TagValueListItems> ValueList
        {
            get { return this._valueList; }
            set { this._valueList = value; }
        }

        [Browsable(false)]
        public Dictionary<string, List<TagValueListItems>> ValueListDictionary
        {
            get { return _valueListDictionary; }
            set
            {
                //if (value != null) { _isList = true; }
                _valueListDictionary = value;
            }
        }

        [Category("ValueList")]
        [Browsable(false)]
        public bool IsList
        {
            get { return this._isList; }
            set { this._isList = value; }
        }


        public TagItem() : this("name", "")
        {
        }

        public TagItem(KeyValuePair<string, string> kvp) : this(kvp.Key, kvp.Value)
        {
        }

        public TagItem(string name, string value) : this(name, value, new Dictionary<string, List<TagValues>>())
        {
        }

        public TagItem(string name, string value, Dictionary<string, List<TagValues>> valueList)
        {
            this.Name = name;
            this.Value = Utils.ConvertToValidTagValue(value);

            //return _valueArray;
            if (valueList.Count > 0)
            {
                List<TagValues> tagValues;
                List<TagValueListItems> tagValueListItems = new List<TagValueListItems>();
                if (valueList.TryGetValue(name, out tagValues))
                {
                    if (!_valueListDictionary.ContainsKey(name))
                    {
                        foreach (var x in tagValues)
                        {
                            tagValueListItems.Add(new TagValueListItems(x.ToString()));
                        }

                        // Sort the value list before adding
                        _valueListDictionary.Add(name, tagValueListItems.OrderBy(o => o.Value).ToList());
                        this.ValueListDictionary = _valueListDictionary;
                    }

                    this._isList = true;
                }

                /*
                foreach (var s in valueList)
                {

                    if (ValueList.Count() < valueList.Count())
                    {
                        //ValueList.Add(new TagValueListItems(s));
                    }
                    _isList = true;
                }
                */
            }

            //ValueList = valueList;
        }


        public override string ToString() => $"{this.Name}";

        public override bool Equals(object obj) => this.Equals(obj as TagItem);

        public bool Equals(TagItem ti) => ti != null && 0 == string.Compare(ti.Name, this.Name, true);

        public override int GetHashCode() => this.Name.GetHashCode();

        public class TagValueListItems
        {
            public string Value;
            public override string ToString() => this.Value;

            public TagValueListItems(string value)
            {
                this.Value = value;
            }

            public TagValueListItems()
            {
            }
        }

        public static List<TagValues> taglist = new List<TagValues>();

        public class TagValueListItemConverter : TypeConverter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                var x = context.Instance.ToString();
                if (_valueListDictionary.ContainsKey(context.Instance.ToString()))
                {
                    return true;
                }

                return false;
            }

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
            {
                return false;
            }

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(_valueListDictionary[context.Instance.ToString()]);
            }

            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                if (sourceType == typeof(string))
                {
                    return true;
                }

                return base.CanConvertFrom(context, sourceType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                if (value is string)
                {
                    //List<TagValueListItems> tvli; = _valueListDictionary[context.Instance.ToString()];
                    List<TagValueListItems> tvli;
                    _valueListDictionary.TryGetValue(context.Instance.ToString(), out tvli);

                    // If the value is not in the list dictionary, just return the string value
                    if (tvli == null)
                    {
                        return value;
                    }

                    // If the dictionary has the right key, try to get the value selected from the dictionary
                    foreach (TagValueListItems tv in tvli)
                    {
                        if (tv.Value == (string)value)
                        {
                            return tv.Value;
                        }
                    }

                    // If the value was not in the dictionary, return the value anyway.
                    return value;
                }

                return base.ConvertFrom(context, culture, value);
            }
            /*
            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                if (destinationType == typeof(string))
                {
                    return true;
                }
                return base.CanConvertFrom(context, destinationType);
            }

            public override bool IsValid(ITypeDescriptorContext context, object value)
            {
                return true;
            }
            */
        }
    }
}