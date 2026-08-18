param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectRoot = Join-Path $repoRoot "DXDisplayCleanup"
$distRoot = Join-Path $repoRoot "dist"
$packageRoot = Join-Path $distRoot "DX Companion"
$zipPath = Join-Path $distRoot "DX-Companion-v1.4.1.zip"
$sourceApk = Join-Path $projectRoot "app\build\outputs\apk\release\app-release.apk"
$expectedPackage = "io.github.mazemei.dxdisplaycleanup"
$expectedPermissions = @(
    "android.permission.WRITE_SECURE_SETTINGS",
    "android.permission.FOREGROUND_SERVICE",
    "android.permission.FOREGROUND_SERVICE_DATA_SYNC",
    "android.permission.FOREGROUND_SERVICE_CONNECTED_DEVICE",
    "android.permission.CHANGE_NETWORK_STATE",
    "android.permission.ACCESS_NETWORK_STATE",
    "android.permission.INTERNET",
    "android.permission.POST_NOTIFICATIONS",
    "android.permission.WAKE_LOCK"
)
$expectedCertificate = "ad615803c63760439750c36801e8152ab8664c60ee481ef1473f1df5e80733be"

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childPath = [IO.Path]::GetFullPath($Child)
    if (!$childPath.StartsWith(
        $parentPath,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the distribution folder: $childPath"
    }
}

if (!$SkipBuild) {
    & (Join-Path $PSScriptRoot "Build-AndroidCleanup.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Android build helper failed with exit code $LASTEXITCODE."
    }
}

if (!(Test-Path -LiteralPath $sourceApk -PathType Leaf)) {
    throw "Release APK is missing: $sourceApk"
}

$androidHome = $env:ANDROID_HOME
if (!$androidHome) {
    $androidHome = Join-Path $repoRoot ".build-tools\android\sdk"
}
$buildTools = Get-ChildItem -LiteralPath (Join-Path $androidHome "build-tools") `
    -Directory -ErrorAction SilentlyContinue |
    Where-Object {
        (Test-Path -LiteralPath (Join-Path $_.FullName "aapt.exe")) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName "apksigner.bat"))
    } |
    Sort-Object { [Version]$_.Name } -Descending |
    Select-Object -First 1
if (!$buildTools) {
    throw "Android build-tools with aapt and apksigner were not found."
}

if (!$env:JAVA_HOME) {
    $localJdk = Get-ChildItem -LiteralPath (Join-Path $repoRoot ".build-tools\android\jdk") `
        -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "bin\java.exe") } |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if ($localJdk) { $env:JAVA_HOME = $localJdk.FullName }
}

$aapt = Join-Path $buildTools.FullName "aapt.exe"
$apksigner = Join-Path $buildTools.FullName "apksigner.bat"
$badging = @(& $aapt dump badging $sourceApk)
$badgingText = $badging -join "`n"
if ($LASTEXITCODE -ne 0 -or
    $badgingText -notmatch "package: name='$([Regex]::Escape($expectedPackage))'") {
    throw "Unexpected APK package identity."
}

$permissions = @(& $aapt dump permissions $sourceApk)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect APK permissions."
}
$actualPermissions = @($permissions | Where-Object {
    $_ -match "^uses-permission: name='([^']+)'$"
} | ForEach-Object {
    if ($_ -match "^uses-permission: name='([^']+)'$") { $Matches[1] }
})
$missingPermissions = @($expectedPermissions | Where-Object {
    $actualPermissions -notcontains $_
})
$otherPermissions = @($actualPermissions | Where-Object {
    $expectedPermissions -notcontains $_
})
if ($missingPermissions) {
    throw "Required APK permission is missing: $($missingPermissions -join ', ')"
}
if ($otherPermissions) {
    throw "Unexpected APK permission: $($otherPermissions -join ', ')"
}

$signature = @(& $apksigner verify --verbose --print-certs $sourceApk)
if ($LASTEXITCODE -ne 0) {
    throw "APK signature verification failed."
}
$certificateLine = $signature | Where-Object {
    $_ -match '^Signer #1 certificate SHA-256 digest: ([0-9a-fA-F]+)$'
} | Select-Object -First 1
if (!$certificateLine -or $certificateLine -notmatch '^Signer #1 certificate SHA-256 digest: ([0-9a-fA-F]+)$' -or
    $Matches[1].ToLowerInvariant() -ne $expectedCertificate) {
    throw "Unexpected APK signing certificate."
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
Assert-ChildPath $distRoot $packageRoot
Assert-ChildPath $distRoot $zipPath
if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $sourceApk `
    -Destination (Join-Path $packageRoot "DX-Companion-v1.4.1.apk")
Copy-Item -LiteralPath (Join-Path $projectRoot "PACKAGE_README.md") `
    -Destination (Join-Path $packageRoot "README.md")
Copy-Item -LiteralPath (Join-Path $projectRoot "SIGNING.md") `
    -Destination $packageRoot
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath `
    -CompressionLevel Optimal

Write-Host "Android release folder: $packageRoot"
Write-Host "Android release archive: $zipPath"
