# Run tag.ps1 to create a new tag
. "$PSScriptRoot\tag.ps1"

# Get the latest tag name
$tagName = git describe --tags

# Push the tag to origin
git push origin $tagName
