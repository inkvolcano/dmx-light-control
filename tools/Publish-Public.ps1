<#
  Publishes the current code to the public GitHub repo (inkvolcano/dmx-light-control) and, with -Release,
  creates a GitHub release with the zip from Bouwen.bat (the app's auto-update reads that release).

  The public repo gets a clean copy of HEAD, never this repo's history:
  - left out: original_v1.4/ (the colleague's code), DmxLicht_v1.5.zip, the manufacturers' PDF manuals;
  - commits use GitHub's private noreply address.

  Usage (from the repo root, after committing and running Bouwen.bat):
    powershell -ExecutionPolicy Bypass -File tools\Publish-Public.ps1 -Release -Notes "What's new"
#>
param(
    [switch]$Release,
    [string]$Notes = "",
    [string]$Repo = "inkvolcano/dmx-light-control"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$version = (Get-Content version.txt -Raw).Trim()
$pub = Join-Path $root "publish\_public"
$exclude = @("original_v1.4", "DmxLicht_v1.5.zip")

$login = gh api user --jq .login
$id = gh api user --jq .id
$email = "$id+$login@users.noreply.github.com"

# 1. a working copy of the public repo
$exists = $true
try { gh repo view $Repo --json name | Out-Null } catch { $exists = $false }
if ($LASTEXITCODE -ne 0) { $exists = $false }
if (-not (Test-Path (Join-Path $pub ".git"))) {
    if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
    if ($exists) { git clone "https://github.com/$Repo.git" $pub } else { New-Item -ItemType Directory $pub | Out-Null; git -C $pub init -b main | Out-Null }
}

# 2. replace its contents by HEAD of this repo, without the private files
Get-ChildItem $pub -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force
$zip = Join-Path $env:TEMP "dmxlicht-public-$version.zip"
git archive --format=zip -o $zip HEAD
Expand-Archive $zip -DestinationPath $pub -Force
Remove-Item $zip
foreach ($x in $exclude) { $p = Join-Path $pub $x; if (Test-Path $p) { Remove-Item $p -Recurse -Force } }
Get-ChildItem $pub -Recurse -Filter *.pdf | Remove-Item -Force

# 3. commit with the noreply identity and push
git -C $pub add -A
$changes = git -C $pub status --porcelain
if ($changes) {
    git -C $pub -c user.name=$login -c user.email=$email commit -q -m "DMX Light Control $version"
} else { Write-Host "No code changes." }
if (-not $exists) {
    gh repo create $Repo --public --source $pub --push --description "DMX lighting control for Windows: uDMX, Art-Net, sACN, effects, timeline, phone control, Home Assistant, ambilight" | Out-Null
} else {
    git -C $pub push -q origin HEAD:main
}

# 4. release with the zip and its checksum (auto-update downloads these)
if ($Release) {
    $z = "publish\DMX-Light-Control-$version.zip"; $s = "$z.sha256"
    if (-not (Test-Path $z) -or -not (Test-Path $s)) { throw "Run Bouwen.bat first: $z and $s are needed." }
    if (-not $Notes) { $Notes = "DMX Light Control $version" }
    # the notes go through a file: quotes in them can never break the command line
    $nf = Join-Path $env:TEMP "dlc-release-notes.md"
    [IO.File]::WriteAllText($nf, $Notes, (New-Object Text.UTF8Encoding $false))
    # installs up to 2.1.16 only look for DmxLicht-<version>.zip: the same zip under that name too, labelled so
    # nobody downloads it by hand (the label is what the release page shows)
    $old = Join-Path $env:TEMP "DmxLicht-$version.zip"; $oldS = "$old.sha256"
    Copy-Item $z $old -Force
    $h = (Get-FileHash $old -Algorithm SHA256).Hash.ToLower()
    [IO.File]::WriteAllText($oldS, "$h  DmxLicht-$version.zip`n")
    gh release create "v$version" $z $s "$old#Auto-update for 2.1.16 and older (zip)" "$oldS#Auto-update for 2.1.16 and older (checksum)" `
        --repo $Repo --title "DMX Light Control $version" --notes-file $nf --target main
    Remove-Item $nf, $old, $oldS -ErrorAction SilentlyContinue
}
Write-Host "Done: https://github.com/$Repo"
