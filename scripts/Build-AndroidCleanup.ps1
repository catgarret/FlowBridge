param(
    [switch]$Debug,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectRoot = Join-Path $repoRoot "DXDisplayCleanup"
$localAndroidRoot = Join-Path $repoRoot ".build-tools\android"
$localGradleHome = Join-Path $localAndroidRoot "gradle-home"

if (!$env:JAVA_HOME) {
    $localJdk = Get-ChildItem -LiteralPath (Join-Path $localAndroidRoot "jdk") `
        -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "bin\java.exe") } |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if ($localJdk) {
        $env:JAVA_HOME = $localJdk.FullName
    }
}

if (!$env:ANDROID_HOME) {
    $localSdk = Join-Path $localAndroidRoot "sdk"
    if (Test-Path -LiteralPath $localSdk) {
        $env:ANDROID_HOME = $localSdk
    }
}

if (!$env:GRADLE_USER_HOME) {
    New-Item -ItemType Directory -Force $localGradleHome | Out-Null
    $env:GRADLE_USER_HOME = $localGradleHome
}

if (!$env:JAVA_HOME -or
    !(Test-Path -LiteralPath (Join-Path $env:JAVA_HOME "bin\java.exe"))) {
    throw "JDK 17 was not found. Set JAVA_HOME or install it under .build-tools\android\jdk."
}

if (!$env:ANDROID_HOME -or
    !(Test-Path -LiteralPath (Join-Path $env:ANDROID_HOME "platforms\android-36"))) {
    throw "Android SDK platform 36 was not found. Set ANDROID_HOME or install it under .build-tools\android\sdk."
}

if (!$Debug) {
    $signingProperties = Join-Path $projectRoot "signing.properties"
    if (!(Test-Path -LiteralPath $signingProperties -PathType Leaf)) {
        throw "Release signing.properties is missing. Refusing to create an unsigned or stale Release APK."
    }
    $previousReleaseApk = Join-Path $projectRoot `
        "app\build\outputs\apk\release\app-release.apk"
    if (Test-Path -LiteralPath $previousReleaseApk) {
        Remove-Item -LiteralPath $previousReleaseApk -Force
    }
}

$tasks = @()
if (!$SkipTests) {
    $tasks += "testDebugUnitTest"
}
$tasks += if ($Debug) {
    @("lintDebug", "assembleDebug")
}
else {
    @("lintRelease", "assembleRelease")
}

Push-Location $projectRoot
try {
    & ".\gradlew.bat" @tasks "--no-daemon"
    if ($LASTEXITCODE -ne 0) {
        throw "Android build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$variant = if ($Debug) { "debug" } else { "release" }
$apkName = if ($Debug) { "app-debug.apk" } else { "app-release.apk" }
$apkPath = Join-Path $projectRoot "app\build\outputs\apk\$variant\$apkName"
if (!(Test-Path -LiteralPath $apkPath)) {
    throw "The expected APK was not created: $apkPath"
}

Get-Item -LiteralPath $apkPath |
    Select-Object FullName, Length, LastWriteTime
