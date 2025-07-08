// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Core
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    ///     Generates Random numbers between a range using the implementation of Cryptographic Service Provider
    /// </summary>
    public sealed class CryptoRandomGenerator : IDisposable
    {
        private readonly RNGCryptoServiceProvider rngCsp;

        public CryptoRandomGenerator()
        {
            this.rngCsp = new RNGCryptoServiceProvider();
        }

        /// <summary>
        ///     Dispose of CryptoRandomGenerator
        /// </summary>
        public void Dispose()
        {
            this.rngCsp.Dispose();
        }

        /// <summary>
        ///     Generates random numbers between minValue and maxValue, inclusive of minValue and exclusive of maxValue.
        /// </summary>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        public int Next(int minValue, int maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException("minValue");
            }

            if (minValue == maxValue - 1)
            {
                return minValue;
            }

            var diff = maxValue - minValue;
            var max = uint.MaxValue / diff * diff;

            while (true)
            {
                var rand = this.GetRandomUInt();

                if (rand < max)
                {
                    return (int)(minValue + rand % diff);
                }
            }
        }

        public int Next(int maxVal)
        {
            return this.Next(0, maxVal);
        }

        private uint GetRandomUInt()
        {
            // Create a byte array to hold the random value.
            byte[] randomNumber = new byte[sizeof(int)];
            // Fill the array with a random value.
            this.rngCsp.GetBytes(randomNumber);
            return BitConverter.ToUInt32(randomNumber, 0);
        }
    }
}