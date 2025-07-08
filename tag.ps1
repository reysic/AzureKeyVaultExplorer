$gitOutput = $(git log -1 --pretty=format:"%h;%cd" --date=iso)
$commitIdDate = $gitOutput.Split(';')
$shortCommitId = $gitOutput[0]
$commitDateUTC = [datetime]::Parse($commitIdDate[1]).ToUniversalTime()
$versionString = $commitDateUTC.ToString("vyyyy.MMdd.HHmm.ss")
git tag $versionString