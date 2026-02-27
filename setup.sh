#!/usr/bin/env bash
# Downloads netcoredbg from https://github.com/Samsung/netcoredbg/releases
# Extracts to ./netcoredbg/
# Usage: bash setup.sh

set -euo pipefail

VERSION="3.1.3-1062"

# Detect platform
OS="$(uname -s)"
case "$OS" in
  Linux*)   ARCHIVE="netcoredbg-linux-x64.tar.gz" ;;
  Darwin*)  ARCHIVE="netcoredbg-osx-x64.tar.gz" ;;
  *)        echo "Unsupported OS: $OS. Use setup.ps1 on Windows." && exit 1 ;;
esac

URL="https://github.com/Samsung/netcoredbg/releases/download/$VERSION/$ARCHIVE"

echo "Downloading netcoredbg $VERSION ($ARCHIVE)..."
curl -fL "$URL" -o netcoredbg.tar.gz

echo "Extracting..."
rm -rf netcoredbg
mkdir -p netcoredbg
tar -xzf netcoredbg.tar.gz -C netcoredbg --strip-components=1 2>/dev/null || \
    tar -xzf netcoredbg.tar.gz -C netcoredbg
rm netcoredbg.tar.gz

chmod +x netcoredbg/netcoredbg 2>/dev/null || true

echo "netcoredbg installed to ./netcoredbg/"
echo "Executable: ./netcoredbg/netcoredbg"
