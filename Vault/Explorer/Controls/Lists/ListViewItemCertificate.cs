// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.Lists
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Dialogs.Certificates;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.PropObjects;

    /// <summary>
    ///     Key Vault Certificate list view item which also presents itself nicely to PropertyGrid
    /// </summary>
    public class ListViewItemCertificate : ListViewItemBase
    {
        public readonly CertificateAttributes Attributes;
        public readonly string Thumbprint;

        private ListViewItemCertificate(ISession session, CertificateIdentifier identifier, CertificateAttributes attributes, string thumbprint, IDictionary<string, string> tags) :
            base(session, KeyVaultCertificatesGroup, identifier, tags, attributes.Enabled, attributes.Created, attributes.Updated, attributes.NotBefore, attributes.Expires)
        {
            this.Attributes = attributes;
            this.Thumbprint = thumbprint?.ToLowerInvariant();
        }

        public ListViewItemCertificate(ISession session, CertificateItem c) : this(session, c.Identifier, c.Attributes, Utils.ByteArrayToHex(c.X509Thumbprint), c.Tags)
        {
        }

        public ListViewItemCertificate(ISession session, CertificateBundle cb) : this(session, cb.CertificateIdentifier, cb.Attributes, Utils.ByteArrayToHex(cb.X509Thumbprint), cb.Tags)
        {
        }

        protected override IEnumerable<PropertyDescriptor> GetCustomProperties()
        {
            yield return new ReadOnlyPropertyDescriptor("Content Type", CertificateContentType.Pfx);
            yield return new ReadOnlyPropertyDescriptor("Thumbprint", this.Thumbprint);
        }

        public override ContentType GetContentType() => ContentType.Pkcs12;

        public override async Task<PropertyObject> GetAsync(CancellationToken cancellationToken)
        {
            var cb = await this.Session.CurrentVault.GetCertificateAsync(this.Name, null, cancellationToken);
            var cert = await this.Session.CurrentVault.GetCertificateWithExportableKeysAsync(this.Name, null, cancellationToken);
            return new PropertyObjectCertificate(cb, cb.Policy, cert, null);
        }

        public override async Task<ListViewItemBase> ToggleAsync(CancellationToken cancellationToken)
        {
            CertificateBundle cb = await this.Session.CurrentVault.UpdateCertificateAsync(this.Name, null, null, new CertificateAttributes { Enabled = !this.Attributes.Enabled }, this.Tags, cancellationToken); // Toggle only Enabled attribute
            return new ListViewItemCertificate(this.Session, cb);
        }

        public override async Task<ListViewItemBase> ResetExpirationAsync(CancellationToken cancellationToken)
        {
            var ca = new CertificateAttributes
            {
                NotBefore = this.NotBefore == null ? null : DateTime.UtcNow.AddHours(-1),
                Expires = this.Expires == null ? null : DateTime.UtcNow.AddYears(1),
            };
            CertificateBundle cb = await this.Session.CurrentVault.UpdateCertificateAsync(this.Name, null, null, ca, this.Tags, cancellationToken); // Reset only NotBefore and Expires attributes
            return new ListViewItemCertificate(this.Session, cb);
        }

        public override async Task<ListViewItemBase> DeleteAsync(CancellationToken cancellationToken)
        {
            await this.Session.CurrentVault.DeleteCertificateAsync(this.Name, cancellationToken);
            return this;
        }

        public override async Task<IEnumerable<object>> GetVersionsAsync(CancellationToken cancellationToken)
        {
            return await this.Session.CurrentVault.GetCertificateVersionsAsync(this.Name, 0, cancellationToken);
        }

        public override Form GetEditDialog(string name, IEnumerable<object> versions)
        {
            return new CertificateDialog(this.Session, name, versions.Cast<CertificateItem>());
        }

        public override async Task<ListViewItemBase> UpdateAsync(object originalObject, PropertyObject newObject, CancellationToken cancellationToken)
        {
            CertificateBundle cb = (CertificateBundle)originalObject;
            PropertyObjectCertificate certNew = (PropertyObjectCertificate)newObject;
            await this.Session.CurrentVault.UpdateCertificatePolicyAsync(certNew.Name, certNew.CertificatePolicy, cancellationToken);
            cb = await this.Session.CurrentVault.UpdateCertificateAsync(certNew.Name, null, null, certNew.ToCertificateAttributes(), certNew.ToTagsDictionary(), cancellationToken);
            return new ListViewItemCertificate(this.Session, cb);
        }

        public static async Task<ListViewItemCertificate> NewAsync(ISession session, PropertyObject newObject, CancellationToken cancellationToken)
        {
            PropertyObjectCertificate certNew = (PropertyObjectCertificate)newObject;
            var certCollection = new X509Certificate2Collection();
            certCollection.Add(certNew.Certificate);
            CertificateBundle cb = await session.CurrentVault.ImportCertificateAsync(certNew.Name, certCollection, certNew.CertificatePolicy, certNew.CertificateBundle.Attributes, certNew.ToTagsDictionary(), cancellationToken);
            return new ListViewItemCertificate(session, cb);
        }
    }
}