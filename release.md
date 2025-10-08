# Release Process

## Steps

1. Generate a formatted git tag from the desired state of `main` by using the `tag.ps1` script in the root of this repo:

   ```text
   .\tag.ps1
   ```

2. Push the tag to GitHub:

   ```text
   git push origin: <tag>
   ```

3. Generate release on GitHub via UI, referencing tag
