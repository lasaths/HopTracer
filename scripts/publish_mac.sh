#!/bin/bash

# .SYNOPSIS
#     Publishes the HopTracer application for macOS.
#
# .DESCRIPTION
#     This script builds the .app bundle for macOS (maccatalyst-x64 and arm64).
#     NOTE: This script MUST be run on a macOS machine. It will fail on Windows.
#
# .EXAMPLE
#     ./scripts/publish_mac.sh

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_FILE="$SCRIPT_DIR/../src_csharp/HopTracer.Maui/HopTracer.Maui.csproj"
RELEASE_DIR="$SCRIPT_DIR/../release"
OUTPUT_DIR="$RELEASE_DIR/HopTracer_Mac"

echo "Build Configuration:"
echo "  Project: $PROJECT_FILE"
echo "  Output:  $OUTPUT_DIR"
echo ""

# Check if running on macOS
if [[ "$OSTYPE" != "darwin"* ]]; then
    echo "ERROR: This script must be run on macOS."
    exit 1
fi

# 1. Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# 2. Publish
# Note: We publish for both x64 and arm64 (Apple Silicon)
# You might want to create a universal binary or separate builds.
# Here we build for Apple Silicon (arm64) as default for modern Macs.
# Change to maccatalyst-x64 for Intel Macs.

TARGET_ARCH="maccatalyst-arm64"

echo "Publishing for $TARGET_ARCH..."
dotnet publish "$PROJECT_FILE" \
    -f net9.0-maccatalyst \
    -c Release \
    -p:CreatePackage=false \
    -p:RuntimeIdentifier=$TARGET_ARCH \
    -o "$OUTPUT_DIR"

echo ""
echo "SUCCESS! App bundle available at:"
echo "  $OUTPUT_DIR/HopTracer.app"
echo ""
echo "NOTE: To distribute this app, you should sign it with an Apple Developer ID."
echo "      Otherwise, users will have to right-click -> Open to bypass Gatekeeper."
