# Release Process

## Steps

1. Generate a formatted git tag from the desired state of `main` and push it to GitHub by using the `tag_and_push.ps1` script in the root of this repo:

   ```text
   .\tag_and_push.ps1
   ```

2. Generate release on GitHub via UI, referencing [tag](https://github.com/reysic/AzureKeyVaultExplorer/tags).
