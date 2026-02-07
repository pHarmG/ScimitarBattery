#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <rid> [configuration]"
  echo "Example: $0 osx-arm64 Release"
  exit 1
fi

RID="$1"
CONFIGURATION="${2:-Release}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/ScimitarBattery/ScimitarBattery.csproj"
PUBLISH_DIR="$ROOT_DIR/publish/$RID"
ARTIFACTS_DIR="$ROOT_DIR/artifacts"
APP_DIR="$ARTIFACTS_DIR/ScimitarBattery.app"
MACOS_DIR="$APP_DIR/Contents/MacOS"
RESOURCES_DIR="$APP_DIR/Contents/Resources"
PLIST_PATH="$APP_DIR/Contents/Info.plist"
ZIP_PATH="$ARTIFACTS_DIR/ScimitarBattery-$RID.zip"

rm -rf "$PUBLISH_DIR" "$APP_DIR"
mkdir -p "$PUBLISH_DIR" "$MACOS_DIR" "$RESOURCES_DIR" "$ARTIFACTS_DIR"

"$DOTNET_BIN" publish "$PROJECT" \
  -c "$CONFIGURATION" \
  -f net8.0 \
  -r "$RID" \
  --self-contained true \
  -o "$PUBLISH_DIR"

# Put all published assets in Contents/MacOS so AppContext.BaseDirectory keeps working.
cp -R "$PUBLISH_DIR"/. "$MACOS_DIR/"

# Remove Finder/AppleDouble noise so release zips are clean.
find "$APP_DIR" -name '.DS_Store' -type f -delete || true
find "$APP_DIR" -name '._*' -type f -delete || true

cat > "$PLIST_PATH" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>Scimitar Battery Monitor</string>
  <key>CFBundleExecutable</key>
  <string>ScimitarBattery</string>
  <key>CFBundleIdentifier</key>
  <string>com.scimitarbattery.monitor</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>ScimitarBattery</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

cat > "$RESOURCES_DIR/Security-Notice.txt" <<'NOTICE'
Scimitar Battery Monitor (unsigned build)

This app is not notarized/signed with an Apple Developer ID.
macOS may block first launch.

Recommended first-run steps:
1) Remove quarantine:
   xattr -dr com.apple.quarantine ScimitarBattery.app
2) Launch:
   open ScimitarBattery.app
3) If blocked, approve in:
   System Settings -> Privacy & Security -> Open Anyway

NOTICE

# Reduce "damaged app" warnings by ensuring no stale quarantine attrs in packaged files.
xattr -dr com.apple.quarantine "$APP_DIR" || true

# Ad-hoc sign for integrity of bundle + nested binaries (still unsigned/not notarized).
codesign --force --deep --sign - "$APP_DIR"
codesign --verify --deep --strict "$APP_DIR"

rm -f "$ZIP_PATH"
(
  cd "$ARTIFACTS_DIR"
  COPYFILE_DISABLE=1 /usr/bin/zip -qry "$(basename "$ZIP_PATH")" "ScimitarBattery.app"
)

echo "Created: $ZIP_PATH"
