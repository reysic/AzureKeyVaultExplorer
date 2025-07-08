// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.ContentTypes
{
    using System.ComponentModel;

    /// <summary>
    ///     See here:
    ///     http://www.freeformatter.com/mime-types-list.html
    ///     http://pki-tutorial.readthedocs.io/en/latest/mime.html
    /// </summary>
    public enum ContentType
    {
        [Description("(none)")]
        None = 0,

        [Description("text/plain")]
        Text,

        [Description("text/csv")]
        Csv,

        [Description("text/tab-separated-values")]
        Tsv,

        [Description("application/xml")]
        Xml,

        [Description("application/json")]
        Json,

        [Description("application/pkix-cert")]
        Certificate,

        [Description("application/x-pkcs12")]
        Pkcs12,

        [Description("application/x-pkcs12b64")]
        Pkcs12Base64,

        [Description("application/x-base64")]
        Base64,

        [Description("application/x-json-gzb64")]
        JsonGZipBase64,

        [Description("application/x-kv-secret")]
        KeyVaultSecret,

        [Description("application/x-kv-certificate")]
        KeyVaultCertificate,

        [Description("application/x-mswinurl")]
        KeyVaultLink,
    }
}