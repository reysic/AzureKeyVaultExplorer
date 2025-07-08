namespace Microsoft.Vault.Explorer.Model.ContentTypes
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;

    public class CustomEnumTypeConverter<T> : EnumConverter where T : struct
    {
        private static readonly Dictionary<T, string> s_toString = new Dictionary<T, string>();

        private static readonly Dictionary<string, T> s_toValue = new Dictionary<string, T>(StringComparer.CurrentCultureIgnoreCase);

        static CustomEnumTypeConverter()
        {
            Debug.Assert(typeof(T).IsEnum, "The custom enum class must be used with an enum type.");
            Initialize();
        }

        public CustomEnumTypeConverter() : base(typeof(T))
        {
        }

        internal static void Initialize()
        {
            foreach (T item in Enum.GetValues(typeof(T)))
            {
                string description = GetDescription(item);
                s_toString[item] = description;
                s_toValue[description] = item;
            }
        }

        public static string GetDescription(T optionValue)
        {
            var optionDescription = optionValue.ToString();
            var optionInfo = typeof(T).GetField(optionDescription);
            if (Attribute.IsDefined(optionInfo, typeof(DescriptionAttribute)))
            {
                var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(optionInfo, typeof(DescriptionAttribute));
                return attribute.Description;
            }

            return optionDescription;
        }

        public static T GetValue(string description)
        {
            if (!string.IsNullOrEmpty(description) && s_toValue.ContainsKey(description))
            {
                return s_toValue[description];
            }

            return default(T);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            var optionValue = (T)value;

            if (destinationType == typeof(string) && s_toString.ContainsKey(optionValue))
            {
                return s_toString[optionValue];
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var stringValue = value as string;

            if (!string.IsNullOrEmpty(stringValue) && s_toValue.ContainsKey(stringValue))
            {
                return s_toValue[stringValue];
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}