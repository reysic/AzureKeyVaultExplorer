// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Common
{
    using System;
    using System.ComponentModel;

    public class ReadOnlyPropertyDescriptor : PropertyDescriptor
    {
        public readonly object Value;

        public ReadOnlyPropertyDescriptor(string name, object value) : base(name, null)
        {
            this.Value = value;
        }

        public override Type PropertyType => this.Value == null ? typeof(string) : this.Value.GetType();

        public override void SetValue(object component, object value)
        {
            throw new InvalidOperationException();
        }

        public override object GetValue(object component) => this.Value;

        public override bool IsReadOnly => true;

        public override Type ComponentType => null;

        public override bool CanResetValue(object component) => false;

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component) => false;
    }
}