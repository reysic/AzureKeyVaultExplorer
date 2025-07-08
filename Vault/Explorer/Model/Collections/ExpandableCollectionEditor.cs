namespace Microsoft.Vault.Explorer.Model.Collections
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.Design;

    /// <summary>
    ///     Our collection editor, that will force refresh the expandable properties in case collection was changed
    /// </summary>
    /// <typeparam name="T">type of the collection</typeparam>
    /// <typeparam name="U">type of the item in the collection</typeparam>
    public class ExpandableCollectionEditor<T, U> : CollectionEditor where T : ObservableCustomCollection<U> where U : class
    {
        public ExpandableCollectionEditor(Type type) : base(type)
        {
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            T oldCollection = value as T;
            bool changed = false;
            var lastHandler = oldCollection.GetLastPropertyChangedEventHandler();
            oldCollection.SetPropertyChangedEventHandler((s, e) => { changed = true; });
            var collection = base.EditValue(context, provider, value);
            // If something was changed in the collection we always return a new value (copy ctor), to force refresh the expandable properties and force PropertyChanged chain
            if (changed)
            {
                T newCollection = (T)Activator.CreateInstance(typeof(T), (IEnumerable<U>)collection);
                newCollection.SetPropertyChangedEventHandler(lastHandler);
                lastHandler.Invoke(newCollection, new PropertyChangedEventArgs("Count"));
                return newCollection;
            }

            return collection;
        }
    }
}