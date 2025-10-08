// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using System.Security.Cryptography;
    using Microsoft.Identity.Client;

    public class MemoryTokenCache
    {
        private static readonly object BufferLock = new object();
        private static byte[] _buffer;

        /// <summary>
        ///     Initializes the cache against an in memory buffer.
        /// </summary>
        public MemoryTokenCache()
        {
        }

        /// <summary>
        ///     Empties the persistent store
        /// </summary>
        public void Clear()
        {
            lock (BufferLock)
            {
                _buffer = null;
            }
        }

        /// <summary>
        ///     Configures the token cache for an MSAL client application
        /// </summary>
        /// <param name="tokenCache">The MSAL token cache to configure</param>
        public void ConfigureTokenCache(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(this.BeforeAccessNotification);
            tokenCache.SetAfterAccess(this.AfterAccessNotification);
        }

        /// <summary>
        ///     Triggered right before MSAL needs to access the cache
        ///     Reload the cache from the persistent store in case it changed since the last access
        /// </summary>
        /// <param name="args"></param>
        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (BufferLock)
            {
                if (_buffer != null)
                {
                    try
                    {
                        byte[] data = ProtectedData.Unprotect(_buffer, null, DataProtectionScope.LocalMachine);
                        args.TokenCache.DeserializeMsalV3(data);
                    }
                    catch (Exception)
                    {
                        // If decryption fails, clear the cache and start fresh
                        this.Clear();
                    }
                }
            }
        }

        /// <summary>
        ///     Triggered right after MSAL accessed the cache
        /// </summary>
        /// <param name="args"></param>
        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                lock (BufferLock)
                {
                    try
                    {
                        // reflect changes in the persistent store
                        byte[] data = args.TokenCache.SerializeMsalV3();
                        _buffer = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
                    }
                    catch (Exception)
                    {
                        // If encryption fails, don't crash the application
                        // Log the error if logging is available
                    }
                }
            }
        }
    }
}