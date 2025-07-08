// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.MenuItems
{
    using System;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Vault.Explorer.Common;

    public abstract class CustomVersion : ToolStripMenuItem
    {
        public readonly int Index;
        public readonly DateTime? Created;
        public readonly DateTime? Updated;
        public readonly string ChangedBy;
        public readonly ObjectIdentifier Id;

        public CustomVersion(int index, DateTime? created, DateTime? updated, string changedBy, ObjectIdentifier id) : base($"{Utils.NullableDateTimeToString(created)}")
        {
            this.Index = index;
            this.Created = created;
            this.Updated = updated;
            this.ChangedBy = changedBy;
            this.Id = id;
            this.ToolTipText = string.Format("Creation time:\t\t{0}\nLast updated time:\t{1}\nChanged by:\t\t{2}\nVersion:\t        {3}",
                Utils.NullableDateTimeToString(this.Created),
                Utils.NullableDateTimeToString(this.Updated),
                this.ChangedBy,
                this.Id.Version);
        }

        public override string ToString() => (0 == this.Index ? "Current value" : $"Value from {this.Text}") + Utils.DropDownSuffix;
    }
}