#!/bin/bash
# Download IBM Plex from official GitHub releases (OFL licensed)
BASE_URL="https://github.com/IBM/plex/releases/download/v6.4.0"

# Download the woff2 files we need
curl -L -o IBMPlexSans-Regular.woff2 "$BASE_URL/IBM-Plex-Sans/fonts/complete/woff2/IBMPlexSans-Regular.woff2"
curl -L -o IBMPlexSans-Medium.woff2 "$BASE_URL/IBM-Plex-Sans/fonts/complete/woff2/IBMPlexSans-Medium.woff2"
curl -L -o IBMPlexSans-SemiBold.woff2 "$BASE_URL/IBM-Plex-Sans/fonts/complete/woff2/IBMPlexSans-SemiBold.woff2"
curl -L -o IBMPlexMono-Regular.woff2 "$BASE_URL/IBM-Plex-Mono/fonts/complete/woff2/IBMPlexMono-Regular.woff2"
curl -L -o IBMPlexMono-Medium.woff2 "$BASE_URL/IBM-Plex-Mono/fonts/complete/woff2/IBMPlexMono-Medium.woff2"

# Download OFL license
curl -L -o LICENSE.txt "https://raw.githubusercontent.com/IBM/plex/master/LICENSE.txt"

echo "IBM Plex fonts downloaded"
