namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;

    public class AccountItem
    {
        public string DomainHint;
        public string UserAlias;

        public AccountItem(string domainHint, string userAlias = null)
        {
            this.DomainHint = domainHint;
            this.UserAlias = userAlias ?? Globals.DefaultUserName;
        }

        public override string ToString() => $"{this.UserAlias}@{this.DomainHint}";
    }
}