namespace Microsoft.Vault.Explorer.Model.Collections
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Globalization;

    /// <summary>
    ///     Shows number of items in the collection in the PropertyGrid item
    /// </summary>
    public class ExpandableCollectionObjectConverter : ExpandableObjectConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) =>
            destinationType == typeof(string) && value is ICollection ? $"{(value as ICollection).Count} item(s)" : base.ConvertTo(context, culture, value, destinationType);
    }
}