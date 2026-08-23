#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"
CACHE="$ROOT/.vendor-cache"
VERSION=4.1
APP_VERSION=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$ROOT/macOS/Info.plist")
ARM_ARCHIVE="$CACHE/scrcpy-aarch64-v$VERSION.tar.gz"
INTEL_ARCHIVE="$CACHE/scrcpy-x86_64-v$VERSION.tar.gz"
WINDOWS_RELEASE="$CACHE/DX-Manager-v2.0.0-win-x64.zip"
mkdir -p "$CACHE"

download_and_verify() {
  url=$1
  output=$2
  expected=$3
  if [ ! -f "$output" ]; then curl -fL "$url" -o "$output"; fi
  actual=$(shasum -a 256 "$output" | awk '{print $1}')
  if [ "$actual" != "$expected" ]; then
    echo "Checksum mismatch: $output" >&2
    exit 1
  fi
}

download_and_verify \
  "https://github.com/Genymobile/scrcpy/releases/download/v$VERSION/scrcpy-macos-aarch64-v$VERSION.tar.gz" \
  "$ARM_ARCHIVE" \
  "20fd47c9014dd5e0fa77091f3cb7adbda8445a360c4584aeaa0150b5b3988ff3"
download_and_verify \
  "https://github.com/Genymobile/scrcpy/releases/download/v$VERSION/scrcpy-macos-x86_64-v$VERSION.tar.gz" \
  "$INTEL_ARCHIVE" \
  "ee2a7223bc8dbdc4f482db1134bcf441178dafb833492b71ca4c22090c58ce72"
download_and_verify \
  "https://github.com/maze-mei/DX-Manager/releases/download/v2.0.0/DX-Manager-v2.0.0-win-x64.zip" \
  "$WINDOWS_RELEASE" \
  "ca78a306f61235708dbdde0541c06e071c646dc0f8b47bc8f2487e5518e8af86"

swift build -c release --arch arm64
swift build -c release --arch x86_64
APP="$ROOT/dist/Flow Bridge.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
lipo -create \
  "$ROOT/.build/arm64-apple-macosx/release/DXManagerMac" \
  "$ROOT/.build/x86_64-apple-macosx/release/DXManagerMac" \
  -output "$APP/Contents/MacOS/DXManagerMac"
cp "$ROOT/macOS/Info.plist" "$APP/Contents/Info.plist"
cp "$ROOT/macOS/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
cp "$ROOT/LICENSE" "$APP/Contents/Resources/FlowBridge-LICENSE.txt"
cp "$ROOT/licenses/DX-MANAGER-MIT.txt" "$APP/Contents/Resources/DX-Manager-MIT-LICENSE.txt"
cp -R "$ROOT/macOS/Resources/en.lproj" "$APP/Contents/Resources/"
cp -R "$ROOT/macOS/Resources/ko.lproj" "$APP/Contents/Resources/"
cp "$ROOT/DexManager/licenses/THIRD_PARTY_NOTICES.md" "$APP/Contents/Resources/THIRD_PARTY_NOTICES.md"
rm -rf "$APP/Contents/Resources/runtime"
rm -rf "$APP/Contents/Resources/companion"
mkdir -p "$APP/Contents/Resources/runtime/arm64" "$APP/Contents/Resources/runtime/x86_64"
tar -xzf "$ARM_ARCHIVE" --strip-components=1 -C "$APP/Contents/Resources/runtime/arm64"
tar -xzf "$INTEL_ARCHIVE" --strip-components=1 -C "$APP/Contents/Resources/runtime/x86_64"
mkdir -p "$APP/Contents/Resources/companion"
unzip -p "$WINDOWS_RELEASE" "DX Manager/tools/companion/DX-Companion.apk" > "$APP/Contents/Resources/companion/DX-Companion.apk"
COMPANION_HASH=$(shasum -a 256 "$APP/Contents/Resources/companion/DX-Companion.apk" | awk '{print $1}')
if [ "$COMPANION_HASH" != "7cd40017789e22440dca0291ab0c45adb564a19d8a623e669f373395536b880f" ]; then
  echo "DX Companion checksum mismatch" >&2
  exit 1
fi
chmod +x "$APP/Contents/Resources/runtime/arm64/adb" "$APP/Contents/Resources/runtime/arm64/scrcpy"
chmod +x "$APP/Contents/Resources/runtime/x86_64/adb" "$APP/Contents/Resources/runtime/x86_64/scrcpy"
codesign --force --deep --sign - "$APP"
rm -f "$ROOT/dist/FlowBridge-macOS-universal-v$APP_VERSION.zip"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$ROOT/dist/FlowBridge-macOS-universal-v$APP_VERSION.zip"
echo "$APP"
