// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    ///     Manages vault configuration persistence to Vaults.json
    /// </summary>
    public static class VaultConfigurationManager
    {
        /// <summary>
        ///     Adds a vault configuration to both Vaults.json and VaultAliases.json files
        /// </summary>
        /// <param name="vaultName">Name of the vault</param>
        /// <param name="vaultAlias">User-friendly alias for the vault</param>
        /// <param name="authMethod">Authentication method (0=Interactive, 1=ClientCredential, 2=Certificate)</param>
        /// <param name="domainHint">Domain hint for authentication</param>
        /// <param name="userAlias">User alias for authentication</param>
        /// <param name="clientId">Client ID (for ClientCredential and Certificate auth)</param>
        /// <param name="clientSecret">Client secret (for ClientCredential auth only)</param>
        /// <param name="certificateThumbprint">Certificate thumbprint (for Certificate auth only)</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool AddVaultConfiguration(
            string vaultName,
            string vaultAlias,
            int authMethod,
            string domainHint,
            string userAlias,
            string clientId = null,
            string clientSecret = null,
            string certificateThumbprint = null)
        {
            try
            {
                // Add vault access configuration to Vaults.json
                string vaultsFilePath = GetVaultsFilePath();
                Dictionary<string, object> vaultsConfig = LoadVaultsConfiguration(vaultsFilePath);

                var vaultAccess = CreateVaultAccessConfiguration(authMethod, domainHint, userAlias, clientId, clientSecret, certificateThumbprint);

                var vaultConfig = new Dictionary<string, object>
                {
                    ["ReadOnly"] = new[] { vaultAccess },
                    ["ReadWrite"] = new[] { vaultAccess },
                };

                string configKey = vaultName.ToLowerInvariant();

                // Check if vault already exists with same configuration
                if (vaultsConfig.ContainsKey(configKey))
                {
                    // Vault already exists - update the configuration instead of creating a duplicate
                    vaultsConfig[configKey] = vaultConfig;
                }
                else
                {
                    // New vault - add it
                    vaultsConfig[configKey] = vaultConfig;
                }

                SaveVaultsConfiguration(vaultsFilePath, vaultsConfig);

                // Add vault alias to VaultAliases.json
                string vaultAliasesFilePath = GetVaultAliasesFilePath();
                var vaultAliases = LoadVaultAliases(vaultAliasesFilePath);

                // Check if alias already exists (safely handle dynamic objects)
                bool aliasExists = false;
                try
                {
                    foreach (var va in vaultAliases)
                    {
                        if (va != null && va.Alias != null &&
                            string.Equals(va.Alias.ToString(), vaultAlias, StringComparison.OrdinalIgnoreCase))
                        {
                            aliasExists = true;
                            break;
                        }
                    }
                }
                catch
                {
                    // If there's any issue accessing dynamic properties, assume alias doesn't exist
                    aliasExists = false;
                }

                if (!aliasExists)
                {
                    var newVaultAlias = new
                    {
                        Alias = vaultAlias,
                        VaultNames = new[] { vaultName.ToLowerInvariant() }, // Use lowercase vault name to match Azure convention
                        SecretKinds = new[]
                        {
                            "Generic",
                            "WD.Certificate",
                            "WD.ServiceFabricService.Configuration.Secret",
                            "WCD.StorageAccount",
                            "WCD.SQLAccount",
                            "WCD.WindowsAccount",
                            "WCD.PfxCertificate",
                            "WCD.CerCertificate",
                            "WCD.ServiceBus",
                            "WCD.DataFactory",
                            "WCD.ApiKey",
                            "WCD.RedisCache",
                            "WCD.AppKey",
                            "Custom",
                        },
                    };

                    vaultAliases.Add(newVaultAlias);
                    SaveVaultAliases(vaultAliasesFilePath, vaultAliases);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add vault configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Gets the full path to the Vaults.json file
        /// </summary>
        private static string GetVaultsFilePath()
        {
            string rootPath = Environment.ExpandEnvironmentVariables(Settings.Default.JsonConfigurationFilesRoot);
            return Path.Combine(rootPath, Settings.Default.VaultsJsonFileLocation);
        }

        /// <summary>
        ///     Loads existing vaults configuration from file
        /// </summary>
        private static Dictionary<string, object> LoadVaultsConfiguration(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                string content = File.ReadAllText(filePath);

                // Extract JSON from template file - look for the actual JSON object at the end
                string json = ExtractJsonFromTemplate(content, "{}");

                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
                {
                    return new Dictionary<string, object>();
                }

                return JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading vaults configuration: {ex.Message}");
                // If file is corrupted, start fresh
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        ///     Saves vaults configuration to file
        /// </summary>
        private static void SaveVaultsConfiguration(string filePath, Dictionary<string, object> config)
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save with pretty formatting
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        ///     Creates vault access configuration object based on authentication method
        /// </summary>
        private static object CreateVaultAccessConfiguration(
            int authMethod,
            string domainHint,
            string userAlias,
            string clientId,
            string clientSecret,
            string certificateThumbprint)
        {
            switch (authMethod)
            {
                case 0: // Interactive
                    return new Dictionary<string, object>
                    {
                        ["$type"] = "Microsoft.Vault.Library.VaultAccessUserInteractive, Microsoft.Vault.Library",
                        ["DomainHint"] = domainHint,
                    };

                case 1: // Client Credential
                    return new Dictionary<string, object>
                    {
                        ["$type"] = "Microsoft.Vault.Library.VaultAccessClientCredential, Microsoft.Vault.Library",
                        ["ClientId"] = clientId,
                        ["ClientSecret"] = clientSecret,
                        ["DomainHint"] = domainHint,
                    };

                case 2: // Certificate
                    return new Dictionary<string, object>
                    {
                        ["$type"] = "Microsoft.Vault.Library.VaultAccessClientCertificate, Microsoft.Vault.Library",
                        ["ClientId"] = clientId,
                        ["CertificateThumbprint"] = certificateThumbprint,
                        ["DomainHint"] = domainHint,
                    };

                default:
                    throw new ArgumentException($"Invalid authentication method: {authMethod}");
            }
        }


        /// <summary>
        ///     Gets the full path to the VaultAliases.json file
        /// </summary>
        private static string GetVaultAliasesFilePath()
        {
            string rootPath = Environment.ExpandEnvironmentVariables(Settings.Default.JsonConfigurationFilesRoot);
            return Path.Combine(rootPath, Settings.Default.VaultAliasesJsonFileLocation);
        }

        /// <summary>
        ///     Loads existing vault aliases from file
        /// </summary>
        private static List<dynamic> LoadVaultAliases(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new List<dynamic>();
            }

            try
            {
                string content = File.ReadAllText(filePath);

                // Extract JSON from template file - look for the actual JSON array at the end
                string json = ExtractJsonFromTemplate(content, "[]");

                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                {
                    return new List<dynamic>();
                }

                return JsonConvert.DeserializeObject<List<dynamic>>(json) ?? new List<dynamic>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading vault aliases: {ex.Message}");
                // If file is corrupted, start fresh
                return new List<dynamic>();
            }
        }

        /// <summary>
        ///     Saves vault aliases to file
        /// </summary>
        private static void SaveVaultAliases(string filePath, List<dynamic> vaultAliases)
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save with pretty formatting
            string json = JsonConvert.SerializeObject(vaultAliases, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        ///     Extracts JSON from template files that contain comments and examples, or returns pure JSON as-is
        ///     Uses modern Newtonsoft.Json comment handling for better reliability
        /// </summary>
        private static string ExtractJsonFromTemplate(string content, string defaultJson)
        {
            try
            {
                string trimmedContent = content.Trim();

                if (string.IsNullOrWhiteSpace(trimmedContent))
                {
                    return defaultJson;
                }

                // Use Newtonsoft.Json with comment handling to parse directly
                var settings = new JsonSerializerSettings
                {
                    // This automatically ignores comments in JSON
                };

                // Try to parse as object first
                if (trimmedContent.StartsWith("{"))
                {
                    try
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(trimmedContent, settings);
                        // If successful, serialize back to clean JSON
                        return JsonConvert.SerializeObject(result);
                    }
                    catch
                    {
                        // Fall through to template extraction logic
                    }
                }
                // Try to parse as array
                else if (trimmedContent.StartsWith("["))
                {
                    try
                    {
                        var result = JsonConvert.DeserializeObject<List<object>>(trimmedContent, settings);
                        // If successful, serialize back to clean JSON
                        return JsonConvert.SerializeObject(result);
                    }
                    catch
                    {
                        // Fall through to template extraction logic
                    }
                }

                // Fallback: Template file logic for complex cases
                // Look for the last occurrence of { or [ that starts actual JSON
                int lastBraceIndex = content.LastIndexOf('{');
                int lastBracketIndex = content.LastIndexOf('[');

                int startIndex = Math.Max(lastBraceIndex, lastBracketIndex);

                if (startIndex == -1)
                {
                    return defaultJson;
                }

                // Extract from the last brace/bracket to the end
                string jsonCandidate = content.Substring(startIndex).Trim();

                // Try to parse with comment handling
                try
                {
                    if (jsonCandidate.StartsWith("{"))
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonCandidate, settings);
                        return JsonConvert.SerializeObject(result);
                    }

                    if (jsonCandidate.StartsWith("["))
                    {
                        var result = JsonConvert.DeserializeObject<List<object>>(jsonCandidate, settings);
                        return JsonConvert.SerializeObject(result);
                    }
                }
                catch
                {
                    // If all parsing attempts fail, return default
                }

                return defaultJson;
            }
            catch
            {
                // If extraction fails, return default
                return defaultJson;
            }
        }
    }
}