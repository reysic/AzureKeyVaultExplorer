namespace Microsoft.Vault.Explorer.Model.ContentTypes
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    public static class ContentTypeUtils
    {
        public static string FromRawValue(this ContentType contentType, string rawValue)
        {
            if (rawValue == null)
            {
                return null;
            }

            switch (contentType)
            {
                case ContentType.None:
                case ContentType.Text:
                case ContentType.Csv:
                case ContentType.Tsv:
                case ContentType.Xml:
                case ContentType.Json:
                case ContentType.Certificate:
                case ContentType.Pkcs12:
                case ContentType.KeyVaultSecret:
                case ContentType.KeyVaultCertificate:
                case ContentType.KeyVaultLink:
                    return rawValue;
                case ContentType.Pkcs12Base64:
                case ContentType.Base64:
                    return Encoding.UTF8.GetString(Convert.FromBase64String(rawValue));
                case ContentType.JsonGZipBase64:
                    // Decode (base64) and decompress the secret raw value
                    var decoded = Convert.FromBase64String(rawValue);
                    using (var input = new MemoryStream(decoded))
                    {
                        using (var output = new MemoryStream())
                        {
                            using (var gz = new GZipStream(input, CompressionMode.Decompress, true))
                            {
                                gz.CopyTo(output);
                            }

                            return Encoding.UTF8.GetString(output.ToArray());
                        }
                    }
                default:
                    throw new ArgumentException($"Invalid ContentType {contentType}");
            }
        }

        public static string ToRawValue(this ContentType contentType, string value)
        {
            if (value == null)
            {
                return null;
            }

            switch (contentType)
            {
                case ContentType.None:
                case ContentType.Text:
                case ContentType.Csv:
                case ContentType.Tsv:
                case ContentType.Xml:
                case ContentType.Json:
                case ContentType.Certificate:
                case ContentType.Pkcs12:
                case ContentType.KeyVaultSecret:
                case ContentType.KeyVaultCertificate:
                case ContentType.KeyVaultLink:
                    return value;
                case ContentType.Pkcs12Base64:
                case ContentType.Base64:
                    return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
                case ContentType.JsonGZipBase64:
                    // Compress and Encode (base64) the secret value
                    using (var input = new MemoryStream(Encoding.UTF8.GetBytes(value)))
                    {
                        using (var output = new MemoryStream())
                        {
                            using (var gz = new GZipStream(output, CompressionMode.Compress, true))
                            {
                                input.CopyTo(gz);
                            }

                            return Convert.ToBase64String(output.ToArray());
                        }
                    }
                default:
                    throw new ArgumentException($"Invalid ContentType {contentType}");
            }
        }

        public static ContentType FromExtension(string extension)
        {
            switch (extension?.ToLowerInvariant())
            {
                case ".txt":
                    return ContentType.Text;
                case ".csv":
                    return ContentType.Csv;
                case ".tsv":
                    return ContentType.Tsv;
                case ".xml":
                case ".config":
                    return ContentType.Xml;
                case ".json":
                    return ContentType.Json;
                case ".cer":
                case ".crt":
                    return ContentType.Certificate;
                case ".pfx":
                case ".p12":
                    return ContentType.Pkcs12;
                case ".pfxb64":
                case ".p12b64":
                    return ContentType.Pkcs12Base64;
                case ".b64":
                case ".base64":
                    return ContentType.Base64;
                case ".gzb64":
                    return ContentType.JsonGZipBase64;
                case ".kv-secret":
                    return ContentType.KeyVaultSecret;
                case ".kv-certificate":
                    return ContentType.KeyVaultCertificate;
                case ".url":
                    return ContentType.KeyVaultLink;
                default:
                    return ContentType.None;
            }
        }

        public static string ToExtension(this ContentType contentType)
        {
            switch (contentType)
            {
                case ContentType.None:
                case ContentType.Base64:
                    return "";
                case ContentType.Text:
                    return ".txt";
                case ContentType.Csv:
                    return ".csv";
                case ContentType.Tsv:
                    return ".tsv";
                case ContentType.Xml:
                    return ".xml";
                case ContentType.Json:
                case ContentType.JsonGZipBase64:
                    return ".json";
                case ContentType.Certificate:
                    return ".cer";
                case ContentType.Pkcs12:
                case ContentType.Pkcs12Base64:
                    return ".pfx";
                case ContentType.KeyVaultSecret:
                    return ".kv-secret";
                case ContentType.KeyVaultCertificate:
                    return ".kv-certificate";
                case ContentType.KeyVaultLink:
                    return ".url";
                default:
                    throw new ArgumentException($"Invalid ContentType {contentType}");
            }
        }

        /// <summary>
        ///     Use to set right FilterIndex as part of SaveFileDialog flow
        ///     Text files|*.txt|Csv (Comma delimited)|*.csv|Tsv (Tab delimited)|*.tsv|Configuration
        ///     files|*.json;*.xml;*.config|X509 Certificate|*.cer;*.crt|Personal Information Exchange|*.pfx;*.p12|Key Vault Secret
        ///     files|*.kv-secret|Key Vault Certificate files|*.kv-certificate|All files|*.*
        /// </summary>
        public static int ToFilterIndex(this ContentType contentType)
        {
            switch (contentType)
            {
                case ContentType.Text:
                    return 1;
                case ContentType.Csv:
                    return 2;
                case ContentType.Tsv:
                    return 3;
                case ContentType.Xml:
                case ContentType.Json:
                case ContentType.JsonGZipBase64:
                    return 4;
                case ContentType.Certificate:
                    return 5;
                case ContentType.Pkcs12:
                case ContentType.Pkcs12Base64:
                    return 6;
                case ContentType.KeyVaultSecret:
                    return 7;
                case ContentType.KeyVaultCertificate:
                    return 8;
                case ContentType.KeyVaultLink:
                    return 9;
                case ContentType.None:
                case ContentType.Base64:
                    return 10;
                default:
                    throw new ArgumentException($"Invalid ContentType {contentType}");
            }
        }

        public static string ToSyntaxHighlightingMode(this ContentType contentType)
        {
            switch (contentType)
            {
                case ContentType.None:
                case ContentType.Text:
                case ContentType.Csv:
                case ContentType.Tsv:
                    return "ASP/XHTML";
                case ContentType.Xml:
                    return "XML";
                case ContentType.Json:
                case ContentType.Certificate:
                case ContentType.Pkcs12:
                case ContentType.Pkcs12Base64:
                case ContentType.JsonGZipBase64:
                case ContentType.KeyVaultSecret:
                case ContentType.KeyVaultCertificate:
                case ContentType.KeyVaultLink:
                    return "JavaScript";
                case ContentType.Base64:
                    return "HTML";
                default:
                    throw new ArgumentException($"Invalid ContentType {contentType}");
            }
        }

        /// <summary>
        ///     True if content type is certificate, otherwise False
        /// </summary>
        public static bool IsCertificate(this ContentType contentType) => contentType == ContentType.Certificate || contentType == ContentType.Pkcs12 || contentType == ContentType.Pkcs12Base64;
    }
}