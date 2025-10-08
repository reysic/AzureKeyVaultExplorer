// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Identity.Client;
    using Microsoft.Vault.Core;
    using Newtonsoft.Json;

    [JsonObject]
    public abstract class VaultAccess
    {
        private static readonly SemaphoreSlim _authSemaphore = new SemaphoreSlim(1, 1);

        [JsonProperty]
        public readonly string ClientId; // Also known as ApplicationId, see Get-AzureRmADApplication

        [JsonIgnore]
        public readonly int Order;

        public VaultAccess(string clientId, int order)
        {
            Guid r;
            if (!Guid.TryParseExact(clientId, "D", out r))
            {
                throw new ArgumentException($"{clientId} must be a valid GUID in the following format: 00000000-0000-0000-0000-000000000000", nameof(clientId));
            }

            this.ClientId = clientId;
            this.Order = order;
        }

        protected abstract Task<AuthenticationResult> AcquireTokenInternalAsync(string[] scopes, string userAlias = "");

        public async Task<AuthenticationResult> AcquireTokenAsync(string[] scopes, string userAlias = "")
        {
            await _authSemaphore.WaitAsync();
            try
            {
                try
                {
                    // First, try to get a token silently
                    return await this.AcquireTokenSilentAsync(scopes, userAlias);
                }
                catch (MsalUiRequiredException)
                {
                }

                // Silent token acquisition failed, fallback to interactive/credential flow
                return await this.AcquireTokenInternalAsync(scopes, userAlias);
            }
            finally
            {
                _authSemaphore.Release();
            }
        }

        protected abstract Task<AuthenticationResult> AcquireTokenSilentAsync(string[] scopes, string userAlias = "");

        // Helper method to convert resource URL to scope
        public static string[] ConvertResourceToScopes(string resource)
        {
            if (string.IsNullOrEmpty(resource))
            {
                return new string[0];
            }

            // Convert ADAL resource URLs to MSAL scopes
            if (resource.EndsWith("/"))
            {
                resource = resource.TrimEnd('/');
            }

            return new[] { $"{resource}/.default" };
        }
    }

    [JsonObject]
    public class VaultAccessUserInteractive : VaultAccess
    {
        internal const string PowerShellApplicationId = "1950a258-227b-4e31-a9cf-717495945fc2";

        [JsonProperty]
        public readonly string DomainHint;

        [JsonProperty]
        public readonly string UserAliasType;

        private IPublicClientApplication _publicClientApp;

        public VaultAccessUserInteractive(string domainHint) : base(PowerShellApplicationId, 2)
        {
            this.DomainHint = string.IsNullOrEmpty(domainHint) ? "common" : domainHint;
        }

        [JsonConstructor]
        public VaultAccessUserInteractive(string domainHint, string UserAlias) : base(PowerShellApplicationId, 2)
        {
            this.DomainHint = string.IsNullOrEmpty(domainHint) ? "common" : domainHint;
            this.UserAliasType = UserAlias ?? string.Empty;
        }

        private IPublicClientApplication GetPublicClientApp()
        {
            if (this._publicClientApp == null)
            {
                var builder = PublicClientApplicationBuilder
                    .Create(this.ClientId)
                    .WithAuthority($"https://login.microsoftonline.com/{this.DomainHint}")
                    .WithRedirectUri("http://localhost")
                    .WithDefaultRedirectUri();

                // Disable broker to avoid WAM issues
                try
                {
                    // Try the new way first (MSAL 4.61+)
                    this._publicClientApp = builder.Build();
                }
                catch
                {
                    // Fallback for older MSAL versions
                    this._publicClientApp = builder.Build();
                }

                // Configure token cache
                var tokenCache = new FileTokenCache(this.DomainHint);
                tokenCache.ConfigureTokenCache(this._publicClientApp.UserTokenCache);
            }

            return this._publicClientApp;
        }

        protected override async Task<AuthenticationResult> AcquireTokenSilentAsync(string[] scopes, string userAlias = "")
        {
            var app = this.GetPublicClientApp();
            var accounts = await app.GetAccountsAsync();

            if (!string.IsNullOrEmpty(userAlias))
            {
                var account = accounts.FirstOrDefault(a => a.Username.StartsWith(userAlias, StringComparison.OrdinalIgnoreCase));
                if (account != null)
                {
                    return await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
                }
            }

            if (accounts.Any())
            {
                return await app.AcquireTokenSilent(scopes, accounts.First()).ExecuteAsync();
            }

            throw new MsalUiRequiredException("no_account", "No account found in cache");
        }

        protected override async Task<AuthenticationResult> AcquireTokenInternalAsync(string[] scopes, string userAlias = "")
        {
            if (false == Environment.UserInteractive)
            {
                throw new VaultAccessException($@"Current process PID: {Process.GetCurrentProcess().Id} is running in non user interactive mode. Username: {Environment.UserDomainName}\{Environment.UserName} Machine name: {Environment.MachineName}");
            }

            var app = this.GetPublicClientApp();
            var builder = app.AcquireTokenInteractive(scopes)
                .WithUseEmbeddedWebView(false); // Use system browser instead of embedded

            // Attempt to login with provided user alias
            if (!string.IsNullOrEmpty(userAlias))
            {
                if (userAlias.Contains('@'))
                {
                    builder = builder.WithLoginHint($"{userAlias}");
                }
                else
                {
                    builder = builder.WithLoginHint($"{userAlias}@{this.DomainHint}");
                }

            }

            return await builder.ExecuteAsync();
        }

        public override string ToString() => $"{nameof(VaultAccessUserInteractive)}";
    }

    [JsonObject]
    public class VaultAccessClientCredential : VaultAccess
    {
        [JsonProperty]
        public readonly string ClientSecret;

        private IConfidentialClientApplication _confidentialClientApp;

        [JsonConstructor]
        public VaultAccessClientCredential(string clientId, string clientSecret) : base(clientId, 1)
        {
            Guard.ArgumentNotNull(clientSecret, nameof(clientSecret));
            this.ClientSecret = clientSecret;
        }

        private IConfidentialClientApplication GetConfidentialClientApp()
        {
            if (this._confidentialClientApp == null)
            {
                this._confidentialClientApp = ConfidentialClientApplicationBuilder
                    .Create(this.ClientId)
                    .WithClientSecret(this.ClientSecret)
                    .WithAuthority("https://login.microsoftonline.com/common")
                    .Build();

                // Configure token cache
                var tokenCache = new MemoryTokenCache();
                tokenCache.ConfigureTokenCache(this._confidentialClientApp.AppTokenCache);
            }

            return this._confidentialClientApp;
        }

        protected override async Task<AuthenticationResult> AcquireTokenSilentAsync(string[] scopes, string userAlias = "")
        {
            var app = this.GetConfidentialClientApp();
            return await app.AcquireTokenForClient(scopes).ExecuteAsync();
        }

        protected override async Task<AuthenticationResult> AcquireTokenInternalAsync(string[] scopes, string userAlias = "")
        {
            var app = this.GetConfidentialClientApp();
            return await app.AcquireTokenForClient(scopes).ExecuteAsync();
        }

        public override string ToString() => $"{nameof(VaultAccessClientCredential)}";
    }

    [JsonObject]
    public class VaultAccessClientCertificate : VaultAccess
    {
        [JsonProperty]
        public readonly string CertificateThumbprint;

        private X509Certificate2 _certificate;
        private IConfidentialClientApplication _confidentialClientApp;

        [JsonConstructor]
        public VaultAccessClientCertificate(string clientId, string certificateThumbprint) : base(clientId, 0)
        {
            Guard.ArgumentIsSha1(certificateThumbprint, nameof(certificateThumbprint));
            this.CertificateThumbprint = certificateThumbprint;
            this._certificate = null;
        }

        private IEnumerable<X509Store> EnumerateX509Stores()
        {
            yield return new X509Store(StoreName.My, StoreLocation.CurrentUser);
            yield return new X509Store(StoreName.My, StoreLocation.LocalMachine);
        }

        private void FindCertificate()
        {
            if (null != this._certificate)
            {
                return;
            }

            foreach (var store in this.EnumerateX509Stores())
            {
                try
                {
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                    var storeCerts = store.Certificates.Find(X509FindType.FindByThumbprint, this.CertificateThumbprint, false);
                    if (storeCerts != null && storeCerts.Count > 0)
                    {
                        this._certificate = storeCerts[0];
                        break;
                    }
                }
                finally
                {
                    store.Close();
                }
            }
        }

        [JsonIgnore]
        public X509Certificate2 Certificate
        {
            get
            {
                this.FindCertificate();
                if (this._certificate == null)
                {
                    throw new CertificateNotFoundException($@"Certificate {this.CertificateThumbprint} is not installed in CurrentUser\My or in LocalMachine\My stores. Username: {Environment.UserDomainName}\{Environment.UserName} Machine name: {Environment.MachineName}");
                }

                return this._certificate;
            }
        }

        private IConfidentialClientApplication GetConfidentialClientApp()
        {
            if (this._confidentialClientApp == null)
            {
                this._confidentialClientApp = ConfidentialClientApplicationBuilder
                    .Create(this.ClientId)
                    .WithCertificate(this.Certificate)
                    .WithAuthority("https://login.microsoftonline.com/common")
                    .Build();

                // Configure token cache
                var tokenCache = new MemoryTokenCache();
                tokenCache.ConfigureTokenCache(this._confidentialClientApp.AppTokenCache);
            }

            return this._confidentialClientApp;
        }

        protected override async Task<AuthenticationResult> AcquireTokenSilentAsync(string[] scopes, string userAlias = "")
        {
            var app = this.GetConfidentialClientApp();
            return await app.AcquireTokenForClient(scopes).ExecuteAsync();
        }

        protected override async Task<AuthenticationResult> AcquireTokenInternalAsync(string[] scopes, string userAlias = "")
        {
            var app = this.GetConfidentialClientApp();
            return await app.AcquireTokenForClient(scopes).ExecuteAsync();
        }

        public override string ToString() => $"{nameof(VaultAccessClientCertificate)}";
    }
}