// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model
{
    using System;
    using System.IO;

    /// <summary>
    ///     Deletes file if file location is under %TEMP% folder, to be used with using pattern
    /// </summary>
    public class DeleteTempFileInfo : IDisposable
    {
        public FileInfo FileInfoObject { get; set; }

        public void Dispose()
        {
            if (this.FileInfoObject != null)
            {
                if (this.FileInfoObject.DirectoryName.StartsWith(Path.GetTempPath().TrimEnd('\\'), StringComparison.CurrentCultureIgnoreCase))
                {
                    this.FileInfoObject.Delete();
                }

                this.FileInfoObject = null;
            }
        }
    }
}