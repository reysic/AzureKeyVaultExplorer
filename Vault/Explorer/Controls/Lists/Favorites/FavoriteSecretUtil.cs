namespace Microsoft.Vault.Explorer.Controls.Lists.Favorites
{
    public static class FavoriteSecretUtil
    {
        public static bool Contains(string vaultAlias, string secretName)
        {
            return Settings.Default.FavoriteSecretsDictionary.ContainsKey(vaultAlias) ? Settings.Default.FavoriteSecretsDictionary[vaultAlias].ContainsKey(secretName) ? true : false : false;
        }

        public static void Add(string vaultAlias, string secretName)
        {
            if (false == Settings.Default.FavoriteSecretsDictionary.ContainsKey(vaultAlias))
            {
                Settings.Default.FavoriteSecretsDictionary.Add(vaultAlias, new FavoriteSecrets());
            }

            var favorites = Settings.Default.FavoriteSecretsDictionary[vaultAlias];
            favorites.Add(secretName, new FavoriteSecret());
        }

        public static void Remove(string vaultAlias, string secretName)
        {
            if (Settings.Default.FavoriteSecretsDictionary.ContainsKey(vaultAlias))
            {
                var favorites = Settings.Default.FavoriteSecretsDictionary[vaultAlias];
                favorites.Remove(secretName);
                if (favorites.Count == 0)
                {
                    Settings.Default.FavoriteSecretsDictionary.Remove(vaultAlias);
                }
            }
        }
    }
}