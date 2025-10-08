// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Rest.Azure;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;

    /// <summary>
    ///     User experience operation, to be used with using() keyword
    /// </summary>
    public class UxOperation : IDisposable
    {
        public static AsyncLocal<bool> WasUserCancelled = new AsyncLocal<bool>();

        public CancellationToken CancellationToken => this._cancellationTokenSource.Token;

        private readonly DateTimeOffset _startTime;
        private readonly VaultAlias _currentVaultAlias;
        private readonly ToolStripItem _statusLabel;
        private readonly ToolStripProgressBar _statusProgress;
        private readonly ToolStripItem _cancelButton;
        private readonly ToolStripItem[] _controlsToToggle;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private bool _disposedValue; // To detect redundant calls

        public UxOperation(VaultAlias currentVaultAlias, ToolStripItem statusLabel, ToolStripProgressBar statusProgress, ToolStripItem cancelButton, params ToolStripItem[] controlsToToggle)
        {
            this._startTime = DateTimeOffset.UtcNow;
            this._currentVaultAlias = currentVaultAlias;
            this._statusLabel = statusLabel;
            this._statusProgress = statusProgress;
            this._cancelButton = cancelButton;
            this._controlsToToggle = controlsToToggle;

            this._cancellationTokenSource = new CancellationTokenSource();

            ToggleControls(false, this._controlsToToggle);
            this._statusLabel.Text = "Busy";
            this.ProgressBarVisibility(true);
            if (this._cancelButton != null)
            {
                this._cancelButton.Click += this.uxButtonCancel_Click;
            }

            Cursor.Current = Cursors.WaitCursor;
        }

        public void Dispose()
        {
            if (this._disposedValue)
            {
                return;
            }

            if (this._cancelButton != null)
            {
                this._cancelButton.Click -= this.uxButtonCancel_Click;
            }

            this._cancellationTokenSource.Dispose();
            ToggleControls(true, this._controlsToToggle);
            this._statusLabel.Text = "Ready";
            this.ProgressBarVisibility(false);

            Cursor.Current = Cursors.Default;
            this._disposedValue = true;
        }

        /// <summary>
        ///     Invoke specified vault releated tasks in parallel, in case all tasks failed with Forbidden code
        ///     show access denied message box. If at least one task finished successfully, no error is showed to user
        /// </summary>
        /// <param name="actionName">Nice name of the action to show in the message box</param>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public async Task Invoke(string actionName, params Func<Task>[] tasks)
        {
            var tasksList = new List<Task>();
            var exceptions = new ConcurrentQueue<Exception>();

            foreach (var t in tasks)
            {
                tasksList.Add(Task.Run(async () =>
                {
                    try
                    {
                        await t();
                    }
                    catch (CloudException ce) when (ce.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        exceptions.Enqueue(ce);
                    }
                    catch (KeyVaultErrorException kvce) when (kvce.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        exceptions.Enqueue(kvce);
                    }
                }));
            }

            await Task.WhenAll(tasksList);
            this.ProgressBarVisibility(false);
            if (exceptions.Count == tasks.Length) // In case all tasks failed with Forbidden, show message box to user
            {
                MessageBox.Show($"Operation to {actionName} {this._currentVaultAlias.Alias} ({string.Join(", ", this._currentVaultAlias.VaultNames)}) denied.\n\nYou are probably missing a certificate in CurrentUser\\My or LocalMachine\\My stores, or you are not part of the appropriate security group.",
                    Globals.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void ToggleControls(bool enabled, params ToolStripItem[] controlsToToggle)
        {
            foreach (var c in controlsToToggle)
            {
                c.Enabled = enabled;
            }
        }

        private void ProgressBarVisibility(bool visible)
        {
            if (this._statusProgress != null)
            {
                this._statusProgress.Visible = visible;
            }

            if (this._cancelButton != null)
            {
                this._cancelButton.Visible = visible;
            }
        }

        private void uxButtonCancel_Click(object sender, EventArgs e)
        {
            WasUserCancelled.Value = true;
            this._cancellationTokenSource.Cancel();
        }
    }
}