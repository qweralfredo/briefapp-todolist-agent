# MCP: Playwright (Browser Automation)

> **Type:** Model Context Protocol (MCP) Server  
> **Protocol:** HTTP / stdio  
> **When to use:** E2E validation of web flows, browser automation, UI testing via AI agent

---

## What It Is

The **Playwright MCP** is an MCP server that exposes Playwright as tools for AI agents. It enables GitHub Copilot and other agents to control a real browser to:

- Navigate web pages and SPA applications
- Fill forms and interact with UI
- Capture screenshots and DOM snapshots
- Execute E2E validations without additional test code
- Intercept network requests

---

## How to Install in VS Code

### Option 1: Via VS Code MCP Settings (recommended)

1. Open `settings.json` (`Ctrl+Shift+P` → `Open User Settings JSON`)
2. Add:

```json
{
  "mcp": {
    "servers": {
      "playwright": {
        "type": "stdio",
        "command": "npx",
        "args": ["@playwright/mcp@latest"]
      }
    }
  }
}
```

3. Save and reload (`Ctrl+Shift+P` → `Developer: Reload Window`)

### Option 2: Global package installation

```bash
npm install -g @playwright/mcp
# or with pnpm
pnpm add -g @playwright/mcp
```

Then configure settings.json pointing to the executable:

```json
{
  "mcp": {
    "servers": {
      "playwright": {
        "type": "stdio",
        "command": "playwright-mcp"
      }
    }
  }
}
```

---

## Install Playwright Browsers

On first run, Playwright needs to download browsers:

```bash
npx playwright install
# Or install only Chromium (lighter):
npx playwright install chromium
```

---

## Available Tools

| Tool | Description |
|---|---|
| `browser_navigate` | Navigate to a URL |
| `browser_snapshot` | Capture an accessible DOM snapshot |
| `browser_take_screenshot` | Take a screenshot |
| `browser_click` | Click an element |
| `browser_type` | Type text into a field |
| `browser_fill_form` | Fill a complete form |
| `browser_select_option` | Select dropdown option |
| `browser_press_key` | Press a keyboard key |
| `browser_hover` | Hover over an element |
| `browser_evaluate` | Execute JavaScript on the page |
| `browser_network_requests` | List network requests |
| `browser_console_messages` | View console messages |
| `browser_tabs` | Manage open tabs |
| `browser_close` | Close the browser |
| `browser_wait_for` | Wait for a condition |
| `browser_handle_dialog` | Handle alerts/confirms |

---

## How to Use in Prompts

### Basic E2E validation

```
Open http://localhost:8400 and verify the page loads correctly.
Take a screenshot and show me.
```

### Full flow test

```
1. Navigate to http://localhost:8400/backlog
2. Click the "New task" button
3. Fill the form with title "E2E Test" and priority "High"
4. Click Save
5. Verify the item appears in the list
6. Take a screenshot of the result
```

### API verification via browser

```
Access http://localhost:8480/api/projects and show me the returned JSON.
```

---

## E2E Validation Flow for This Project

After each implemented feature, use Playwright MCP to validate:

```
1. docker compose up -d  (ensure the stack is running)
   ↓
2. browser_navigate → http://localhost:8400
   ↓
3. browser_snapshot  → verify critical DOM elements
   ↓
4. browser_click / browser_fill_form → simulate user actions
   ↓
5. browser_take_screenshot → evidence of result
   ↓
6. browser_network_requests → verify API calls
```

---

## Advanced Configuration — Headless Mode

For CI environments or headless setups:

```json
{
  "mcp": {
    "servers": {
      "playwright": {
        "type": "stdio",
        "command": "npx",
        "args": ["@playwright/mcp@latest", "--headless"]
      }
    }
  }
}
```

---

## Advanced Configuration — HTTP Port

To run as an HTTP server instead of stdio:

```json
{
  "mcp": {
    "servers": {
      "playwright": {
        "type": "http",
        "url": "http://localhost:3000/mcp"
      }
    }
  }
}
```

Starting the server:

```bash
npx @playwright/mcp@latest --port 3000
```

---

## References

- [Playwright MCP on npm](https://www.npmjs.com/package/@playwright/mcp)
- [Playwright MCP on GitHub](https://github.com/microsoft/playwright-mcp)
- [Playwright Documentation](https://playwright.dev)
- [Model Context Protocol](https://modelcontextprotocol.io)
