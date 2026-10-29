#!/bin/bash
# Get IBM Plex from Google Fonts (simpler, reliable)
# Download the CSS that contains the font URLs
curl -s "https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600&family=IBM+Plex+Mono:wght@400;500&display=swap" \
  | grep -oP 'https://[^)]+' \
  | head -10 \
  | while read url; do
    filename=$(basename "$url")
    curl -sL "$url" -o "$filename"
    echo "Downloaded: $filename"
done

echo "Fonts downloaded from Google Fonts"
ls -lh *.woff2 2>/dev/null || echo "No woff2 files found"
