# Create a new tag
$gitOutput = $(git log -1 --pretty=format:"%h;%cd" --date=iso)
$commitIdDate = $gitOutput.Split(';')
$shortCommitId = $gitOutput[0]
$commitDateUTC = [datetime]::Parse($commitIdDate[1]).ToUniversalTime()
$versionString = $commitDateUTC.ToString('vyyyy.MMdd.HHmm.ss')
git tag $versionString

# Get the latest tag name
$tagName = git describe --tags

# Push the tag to origin
git push origin $tagName
