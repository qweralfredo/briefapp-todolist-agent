#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# Briefapp Box — macOS / Linux Installer v3.0
#
# Installs the Briefapp ecosystem:
#   1. Creates /briefapp (or ~/briefapp if not root) directory structure
#   2. Registers briefapp:// protocol handler (macOS)
#   3. Registers context menus (macOS: Automator Quick Action)
#   4. Copies browser extension to /briefapp/extensions
#   5. Configures MCP connection
#
# Usage:
#   sudo ./installers/install-briefapp.sh          # full install (needs root for /briefapp)
#   ./installers/install-briefapp.sh --user        # installs to ~/briefapp (no root needed)
# ──────────────────────────────────────────────────────────────────────────────

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Parse args
USE_HOME=false
MCP_URL="http://127.0.0.1:8481/mcp"
SKIP_CONTEXT_MENU=false
SKIP_PROTOCOL=false

for arg in "$@"; do
    case $arg in
        --user)        USE_HOME=true ;;
        --mcp-url=*)   MCP_URL="${arg#*=}" ;;
        --skip-menu)   SKIP_CONTEXT_MENU=true ;;
        --skip-proto)  SKIP_PROTOCOL=true ;;
    esac
done

if [ "$USE_HOME" = true ] || [ "$(id -u)" -ne 0 ]; then
    PANDORA_DIR="$HOME/briefapp"
else
    PANDORA_DIR="/briefapp"
fi

echo ""
echo "  ╔══════════════════════════════════════════════════╗"
echo "  ║   Briefapp Box — macOS/Linux Installer v3.0      ║"
echo "  ╚══════════════════════════════════════════════════╝"
echo ""

# ─── 1. Create directory structure ────────────────────────────────────────────
echo "  [1/5] Creating directory structure..."

for dir in "$PANDORA_DIR" "$PANDORA_DIR/extensions" "$PANDORA_DIR/config" "$PANDORA_DIR/logs" "$PANDORA_DIR/handler"; do
    if [ ! -d "$dir" ]; then
        mkdir -p "$dir"
        echo "        Created: $dir"
    else
        echo "        Exists:  $dir"
    fi
done

# ─── 2. Register briefapp:// protocol (macOS only) ────────────────────────────
if [ "$SKIP_PROTOCOL" = false ] && [ "$(uname)" = "Darwin" ]; then
    echo "  [2/5] Registering briefapp:// protocol handler..."

    PLIST_DIR="$HOME/Library/LaunchAgents"
    mkdir -p "$PLIST_DIR"

    # Create a minimal app bundle for protocol handling
    APP_DIR="$HOME/Applications/BriefappHandler.app"
    mkdir -p "$APP_DIR/Contents/MacOS"
    mkdir -p "$APP_DIR/Contents/Resources"

    cat > "$APP_DIR/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.briefapp.handler</string>
    <key>CFBundleName</key>
    <string>Briefapp Handler</string>
    <key>CFBundleVersion</key>
    <string>3.0</string>
    <key>CFBundleExecutable</key>
    <string>briefapp-handler</string>
    <key>CFBundleURLTypes</key>
    <array>
        <dict>
            <key>CFBundleURLName</key>
            <string>Briefapp Protocol</string>
            <key>CFBundleURLSchemes</key>
            <array>
                <string>briefapp</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
PLIST

    cat > "$APP_DIR/Contents/MacOS/briefapp-handler" << HANDLER
#!/usr/bin/env bash
# Briefapp protocol handler — routes briefapp:// URLs
URL="\$1"
python3 "$PANDORA_DIR/handler/egeria_handler.py" "\$URL"
HANDLER
    chmod +x "$APP_DIR/Contents/MacOS/briefapp-handler"

    # Register with Launch Services
    /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -R "$APP_DIR" 2>/dev/null || true

    echo "        Registered: briefapp:// → BriefappHandler.app"
else
    echo "  [2/5] Skipping protocol registration"
fi

# ─── 3. Register Context Menu (macOS: Automator Quick Action) ─────────────────
if [ "$SKIP_CONTEXT_MENU" = false ] && [ "$(uname)" = "Darwin" ]; then
    echo "  [3/5] Creating Quick Action (context menu)..."

    SERVICES_DIR="$HOME/Library/Services"
    mkdir -p "$SERVICES_DIR"

    WORKFLOW_DIR="$SERVICES_DIR/Briefapp Send to Context-Box.workflow"
    mkdir -p "$WORKFLOW_DIR/Contents"

    cat > "$WORKFLOW_DIR/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>NSServices</key>
    <array>
        <dict>
            <key>NSMenuItem</key>
            <dict>
                <key>default</key>
                <string>Briefapp Box: Send to Context-Box</string>
            </dict>
            <key>NSMessage</key>
            <string>runWorkflowAsService</string>
        </dict>
    </array>
</dict>
</plist>
PLIST

    cat > "$WORKFLOW_DIR/Contents/document.wflow" << WFLOW
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>AMApplicationBuild</key>
    <string>523</string>
    <key>AMApplicationVersion</key>
    <string>2.10</string>
    <key>AMDocumentVersion</key>
    <string>2</string>
    <key>actions</key>
    <array>
        <dict>
            <key>action</key>
            <dict>
                <key>AMActionVersion</key>
                <string>1.0</string>
                <key>AMApplication</key>
                <array>
                    <string>Automator</string>
                </array>
                <key>AMBundleIdentifier</key>
                <string>com.apple.RunShellScript</string>
                <key>ActionParameters</key>
                <dict>
                    <key>COMMAND_STRING</key>
                    <string>for f in "$@"; do
    python3 "$PANDORA_DIR/handler/egeria_handler.py" "\$f"
done</string>
                    <key>CheckedForUserDefaultShell</key>
                    <true/>
                    <key>inputMethod</key>
                    <integer>1</integer>
                    <key>shell</key>
                    <string>/bin/bash</string>
                    <key>source</key>
                    <string></string>
                </dict>
            </dict>
        </dict>
    </array>
    <key>connectors</key>
    <dict/>
    <key>workflowMetaData</key>
    <dict>
        <key>workflowTypeIdentifier</key>
        <string>com.apple.Automator.servicesMenu</string>
    </dict>
</dict>
</plist>
WFLOW

    echo "        Created: Quick Action 'Briefapp Box: Send to Context-Box'"
    echo "        Location: $SERVICES_DIR"
else
    echo "  [3/5] Skipping context menu"
fi

# ─── 4. Copy Extensions ──────────────────────────────────────────────────────
echo "  [4/5] Packaging extensions..."

BROWSER_EXT="$REPO_ROOT/extensions/browser-scrapper"
if [ -d "$BROWSER_EXT" ]; then
    TARGET_ZIP="$PANDORA_DIR/extensions/browser-scrapper.zip"
    (cd "$BROWSER_EXT" && zip -r "$TARGET_ZIP" . -x ".*") 2>/dev/null
    echo "        Packaged: browser-scrapper → $TARGET_ZIP"
else
    echo "        [SKIP] Browser extension not found"
fi

# Copy handler
HANDLER_SRC="$REPO_ROOT/extensions/windows-context-menu/egeria_handler.py"
if [ -f "$HANDLER_SRC" ]; then
    cp "$HANDLER_SRC" "$PANDORA_DIR/handler/egeria_handler.py"
    echo "        Copied: egeria_handler.py → $PANDORA_DIR/handler/"
fi

# Create .env if not exists
ENV_FILE="$PANDORA_DIR/handler/.env"
if [ ! -f "$ENV_FILE" ]; then
    cat > "$ENV_FILE" << ENV
# Briefapp Box - Config
PANDORA_MCP_URL=$MCP_URL
PANDORA_BOX_ID=
PANDORA_API_KEY=
MAX_FILE_SIZE_MB=50
ENV
    echo "        Created: $ENV_FILE"
fi

# ─── 5. Configure MCP ────────────────────────────────────────────────────────
echo "  [5/5] Configuring MCP..."

VSCODE_DIR="$REPO_ROOT/.vscode"
mkdir -p "$VSCODE_DIR"
MCP_FILE="$VSCODE_DIR/mcp.json"

cat > "$MCP_FILE" << MCP
{
  "servers": {
    "briefapp-todo-list-mcp": {
      "type": "http",
      "url": "$MCP_URL"
    }
  }
}
MCP

echo "        Configured: $MCP_FILE"

# ─── Summary ─────────────────────────────────────────────────────────────────
echo ""
echo "  ════════════════════════════════════════════════════"
echo "  Installation complete!"
echo ""
echo "  Briefapp directory: $PANDORA_DIR"
echo "  Extensions:        $PANDORA_DIR/extensions/"
echo "  Handler:           $PANDORA_DIR/handler/"
echo "  MCP URL:           $MCP_URL"
echo ""
echo "  Next steps:"
echo "    1. Start the stack: docker compose up -d"
echo "    2. Reload your IDE"
echo "    3. Ask your agent: list my Briefapp projects"
echo ""
