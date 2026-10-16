#Requires -Version 5.1
<#
.SYNOPSIS
  Stage allowlisted World Domination Workshop content (1.6 + 1.7 Assemblies via loadFolders) and optionally upload via SteamCMD.
#>
[CmdletBinding(DefaultParameterSetName = 'Stage')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ChangeNote,

    [Parameter(ParameterSetName = 'Stage')]
    [switch] $StageOnly,

    [Parameter(ParameterSetName = 'Upload')]
    [switch] $Upload,

    [string] $SteamCmdPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RimWorldAppId = '294100'
$DllName = 'TSA_WorldDomination.dll'
$AllowFolders = @(
    'About',
    '1.6',
    '1.7',
    'Defs',
    'Languages',
    'Patches',
    'Textures'
)
$AllowRootFiles = @('loadFolders.xml')
$StageFolders = @('About', '1.6', '1.7', 'Defs', 'Languages', 'Patches', 'Textures')

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$StageRoot = Join-Path $RepoRoot 'tools\_workshop_stage'
$ContentDir = Join-Path $StageRoot 'content'
$VdfPath = Join-Path $StageRoot 'upload.vdf'
$PublishedIdPath = Join-Path $RepoRoot 'About\PublishedFileId.txt'

function Write-Info([string] $Message) { Write-Host $Message }
function Write-Warn([string] $Message) { Write-Warning $Message }

function Get-PublishedFileId {
    if (-not (Test-Path -LiteralPath $PublishedIdPath)) {
        throw "Missing PublishedFileId at: $PublishedIdPath"
    }
    $id = (Get-Content -LiteralPath $PublishedIdPath -Raw).Trim()
    if ($id -notmatch '^\d+$') {
        throw "Invalid PublishedFileId '$id' in $PublishedIdPath"
    }
    return $id
}

function Copy-Tree([string] $RelativeFolder) {
    $src = Join-Path $RepoRoot $RelativeFolder
    $dst = Join-Path $ContentDir $RelativeFolder
    if (-not (Test-Path -LiteralPath $src)) {
        throw "Required folder missing: $RelativeFolder"
    }
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
    $robolog = Join-Path $env:TEMP ("wd1-workshop-robocopy-{0}.log" -f [guid]::NewGuid().ToString('N'))
    $args = @(
        $src, $dst, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP',
        '/XD', '.git', '.vs', 'bin', 'obj',
        '/XF', 'Thumbs.db', 'Desktop.ini', '.DS_Store', '*.pdb', '0Harmony.dll'
    )
    & robocopy @args | Out-File -FilePath $robolog -Encoding utf8
    $code = $LASTEXITCODE
    Remove-Item -LiteralPath $robolog -Force -ErrorAction SilentlyContinue
    if ($code -ge 8) {
        throw "robocopy failed for $RelativeFolder (exit $code)"
    }
}

function Clear-Stage {
    if (Test-Path -LiteralPath $ContentDir) {
        Remove-Item -LiteralPath $ContentDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $ContentDir -Force | Out-Null
}

function Stage-Content {
    Clear-Stage

    foreach ($folder in $StageFolders) {
        Copy-Tree $folder
    }

    foreach ($f in $AllowRootFiles) {
        $src = Join-Path $RepoRoot $f
        if (-not (Test-Path -LiteralPath $src)) {
            throw "Required root file missing: $f"
        }
        Copy-Item -LiteralPath $src -Destination (Join-Path $ContentDir $f) -Force
    }
}

function Assert-VersionDll([string] $VersionFolder) {
    $asmDir = Join-Path $ContentDir "$VersionFolder\Assemblies"
    if (-not (Test-Path -LiteralPath $asmDir)) {
        throw "Missing staged $VersionFolder/Assemblies"
    }
    $asmFiles = @(Get-ChildItem -LiteralPath $asmDir -File -Force | Where-Object { $_.Extension -ieq '.dll' })
    if ($asmFiles.Count -ne 1 -or $asmFiles[0].Name -ne $DllName) {
        $names = ($asmFiles | ForEach-Object { $_.Name }) -join ', '
        throw "$VersionFolder/Assemblies must contain exactly '$DllName' (found: $names)"
    }
}

function Assert-Stage {
    $expected = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]($AllowFolders + $AllowRootFiles),
        [StringComparer]::OrdinalIgnoreCase)
    $actual = @(Get-ChildItem -LiteralPath $ContentDir -Force | ForEach-Object { $_.Name })
    $extra = @($actual | Where-Object { -not $expected.Contains($_) })
    $missingFolders = @($AllowFolders | Where-Object { $n = $_; -not ($actual | Where-Object { $_ -eq $n }) })
    $missingFiles = @($AllowRootFiles | Where-Object { $n = $_; -not ($actual | Where-Object { $_ -eq $n }) })
    if ($extra.Count -gt 0) {
        throw "Stage content has unexpected top-level entries: $($extra -join ', ')"
    }
    if ($missingFolders.Count -gt 0) {
        throw "Stage content missing folders: $($missingFolders -join ', ')"
    }
    if ($missingFiles.Count -gt 0) {
        throw "Stage content missing files: $($missingFiles -join ', ')"
    }

    Assert-VersionDll '1.6'
    Assert-VersionDll '1.7'

    if (-not (Test-Path -LiteralPath (Join-Path $ContentDir 'About\Preview.png'))) {
        throw 'Staged About/Preview.png is missing (required as Steam previewfile).'
    }

    # Stale check against 1.7 DLL
    $dll = Get-Item -LiteralPath (Join-Path $ContentDir "1.7\Assemblies\$DllName")
    $sourceDir = Join-Path $RepoRoot 'Source'
    if (Test-Path -LiteralPath $sourceDir) {
        $newestCs = Get-ChildItem -LiteralPath $sourceDir -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -ne $newestCs -and $dll.LastWriteTimeUtc -lt $newestCs.LastWriteTimeUtc) {
            Write-Warn ("Stale DLL: 1.7/{0} is older than {1}. Rebuild before uploading if you meant to ship code changes." -f $DllName, $newestCs.FullName)
        }
    }
}

function Write-Inventory {
    Write-Info '--- staged top-level ---'
    Get-ChildItem -LiteralPath $ContentDir -Force | ForEach-Object {
        $kind = if ($_.PSIsContainer) { 'dir ' } else { 'file' }
        Write-Info ("  {0}  {1}" -f $kind, $_.Name)
    }

    $files = @(Get-ChildItem -LiteralPath $ContentDir -Recurse -File -Force)
    $bytes = ($files | Measure-Object -Property Length -Sum).Sum
    $mb = [math]::Round($bytes / 1MB, 2)
    Write-Info ("--- {0} files, {1} MB ---" -f $files.Count, $mb)
}

function Escape-VdfString([string] $Value) {
    $s = $Value.Replace('"', "'")
    $s = $s.Replace('\', '\\')
    return $s.Replace("`r`n", '\n').Replace("`n", '\n').Replace("`r", '\n')
}

function Write-UploadVdf([string] $PublishedFileId) {
    $preview = Join-Path $ContentDir 'About\Preview.png'
    $contentEsc = ((Resolve-Path -LiteralPath $ContentDir).Path) -replace '\\', '/'
    $previewEsc = ((Resolve-Path -LiteralPath $preview).Path) -replace '\\', '/'
    $note = Escape-VdfString $ChangeNote

    $vdf = @"
"workshopitem"
{
	"appid"		"$RimWorldAppId"
	"publishedfileid"		"$PublishedFileId"
	"contentfolder"		"$contentEsc"
	"previewfile"		"$previewEsc"
	"changenote"		"$note"
}
"@
    $vdf = $vdf -replace "`r`n", "`n" -replace "`r", "`n"
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($VdfPath, $vdf, $utf8NoBom)
    Write-Info "Wrote VDF: $VdfPath"
}

function Resolve-SteamCmd {
    if ($SteamCmdPath) {
        if (-not (Test-Path -LiteralPath $SteamCmdPath)) {
            throw "SteamCmdPath not found: $SteamCmdPath"
        }
        return (Resolve-Path -LiteralPath $SteamCmdPath).Path
    }
    if ($env:STEAMCMD -and (Test-Path -LiteralPath $env:STEAMCMD)) {
        return (Resolve-Path -LiteralPath $env:STEAMCMD).Path
    }
    $fallback = 'C:\Users\Torben Sandforth\Desktop\Spiele\Steamcmd\steamcmd.exe'
    if (Test-Path -LiteralPath $fallback) { return $fallback }
    throw 'SteamCMD not found. Set -SteamCmdPath or $env:STEAMCMD to steamcmd.exe.'
}

function Invoke-SteamUpload([string] $PublishedFileId) {
    if (-not $env:STEAM_USERNAME) {
        throw 'Set $env:STEAM_USERNAME to the Workshop item owner account before -Upload.'
    }

    Write-Warn 'Quit the Steam client before uploading (same-account SteamCMD login often fails otherwise).'
    Write-Info ("Uploading Workshop item {0} as {1} ..." -f $PublishedFileId, $env:STEAM_USERNAME)

    $cmd = Resolve-SteamCmd
    & $cmd +login $env:STEAM_USERNAME +workshop_build_item $VdfPath +quit
    if ($LASTEXITCODE -ne 0) {
        throw "SteamCMD failed with exit code $LASTEXITCODE"
    }
    Write-Info 'SteamCMD finished.'
}

$doUpload = $Upload.IsPresent
if (-not $doUpload) {
    $StageOnly = $true
}

$publishedId = Get-PublishedFileId
Write-Info "Repo: $RepoRoot"
Write-Info "Workshop publishedfileid: $publishedId"
Write-Info "Staging allowlisted content -> $ContentDir"

Stage-Content
Assert-Stage
Write-Inventory
Write-UploadVdf -PublishedFileId $publishedId

Write-Info ''
Write-Info 'Allowlist: About, 1.6/, 1.7/, Defs, Languages, Patches, Textures, loadFolders.xml'
Write-Info 'Excluded: Source/, root Assemblies/, 0Harmony.dll, .pdb, tools/, git metadata'

if ($doUpload) {
    Invoke-SteamUpload -PublishedFileId $publishedId
}
else {
    Write-Info ''
    Write-Info 'StageOnly complete. Re-run with -Upload to publish.'
}
