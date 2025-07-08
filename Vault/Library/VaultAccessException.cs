// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;

    public class VaultAccessException : AggregateException
    {
        public VaultAccessException(string message, params Exception[] innerExceptions) : base(message, innerExceptions)
        {
        }
    }
}