#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# Briefapp Box — macOS / Linux Uninstaller
#
# Removes:
#   1. briefapp:// protocol handler (macOS)
#   2. Context menu Quick Action (macOS)
#   3. Optionally removes /briefapp directory
#
# Usage:
#   sudo ./installers/uninstall-briefapp.sh             # standard uninstall
#   sudo ./installers/uninstall-briefapp.sh --remove-dir # also removes /briefapp
# ──────────────────────────────────────────────────────────────────────────────

set -euo pipefail

REMOVE_DIR=false

for arg in "$@"; do
    case $arg in
        --remove-dir) REMOVE_DIR=true ;;
    esac
done

if [ "$(id -u)" -ne 0 ]; then
    PANDORA_DIR="$HOME/briefapp"
else
    PANDORA_DIR="/briefapp"
fi

echo ""
echo "  ╔══════════════════════════════════════════════════╗"
echo "  ║   Briefapp Box — macOS/Linux Uninstaller          ║"
echo "  ╚══════════════════════════════════════════════════╝"
echo ""

# ─── 1. Remove protocol handler (macOS) ──────────────────────────────────────
if [ "$(uname)" = "Darwin" ]; then
    echo "  [1/3] Removing briefapp:// protocol handler..."
    APP_DIR="$HOME/Applications/BriefappHandler.app"
    if [ -d "$APP_DIR" ]; then
        rm -rf "$APP_DIR"
        echo "        Removed: $APP_DIR"
    else
        echo "        Not found (already clean)"
    fi
else
    echo "  [1/3] Skipping protocol handler (not macOS)"
fi

# ─── 2. Remove context menu Quick Action (macOS) ─────────────────────────────
if [ "$(uname)" = "Darwin" ]; then
    echo "  [2/3] Removing Quick Action..."
    WORKFLOW="$HOME/Library/Services/Briefapp Send to Context-Box.workflow"
    if [ -d "$WORKFLOW" ]; then
        rm -rf "$WORKFLOW"
        echo "        Removed: $WORKFLOW"
    else
        echo "        Not found (already clean)"
    fi
else
    echo "  [2/3] Skipping Quick Action (not macOS)"
fi

# ─── 3. Remove directory ─────────────────────────────────────────────────────
if [ "$REMOVE_DIR" = true ]; then
    echo "  [3/3] Removing $PANDORA_DIR..."
    if [ -d "$PANDORA_DIR" ]; then
        read -p "        Are you sure you want to delete $PANDORA_DIR? (y/N) " confirm
        if [ "$confirm" = "y" ] || [ "$confirm" = "Y" ]; then
            rm -rf "$PANDORA_DIR"
            echo "        Removed: $PANDORA_DIR"
        else
            echo "        Cancelled."
        fi
    else
        echo "        Not found"
    fi
else
    echo "  [3/3] Keeping $PANDORA_DIR (use --remove-dir to delete)"
fi

echo ""
echo "  Uninstallation complete."
echo ""
