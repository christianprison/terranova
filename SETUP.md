# Terranova – Setup Guide

This guide gets you from a fresh machine to a working development environment in under 60 minutes.

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| Unity Hub + Unity | Latest LTS (check unity.com) | Game engine |
| Git | Latest | Version control |
| Node.js | v20+ LTS | Required for Claude Code + MCP |
| Python | ≥ 3.11 | Required for MCP Unity bridge |
| Claude Code CLI | Latest | AI development assistant |

---

## Step-by-Step Setup

### 1. Install Git (if not present)

**macOS:**
```bash
xcode-select --install
```

**Windows:**
Download from https://git-scm.com/download/win

**Linux (Ubuntu/Debian):**
```bash
sudo apt update && sudo apt install git
```

Verify: `git --version`

### 2. Clone the Repository

```bash
git clone https://github.com/YOUR_USERNAME/terranova.git
cd terranova
```

### 3. Install Unity

1. Download Unity Hub: https://unity.com/download
2. Install Unity Hub
3. In Unity Hub → Installs → Install Editor → Choose latest **LTS** version
4. During installation, add modules:
   - **Windows Build Support** (if on Mac/Linux and want to build for Windows)
   - **Linux Build Support** (if needed)
5. In Unity Hub → Projects → Open → Select the `terranova` folder

### 4. Install Node.js

Download LTS from https://nodejs.org

Verify:
```bash
node --version
npm --version
```

### 5. Install Python ≥ 3.11

**macOS:**
```bash
brew install python@3.11
```

**Windows:**
Download from https://python.org (check "Add to PATH" during install!)

**Linux:**
```bash
sudo apt install python3.11 python3.11-venv
```

Verify: `python3 --version` (or `python --version` on Windows)

### 6. Install Claude Code

```bash
npm install -g @anthropic-ai/claude-code
```

Verify: `claude --version`

### 7. Connect MCP Unity Bridge

```bash
claude mcp add --scope user --transport stdio coplay-mcp \
  --env MCP_TOOL_TIMEOUT=720000 \
  -- uvx --python ">=3.11" coplay-mcp-server@latest
```

Verify:
```bash
claude mcp list
```

You should see `coplay-mcp` listed.

### 8. Verify Everything Works

1. Open the Terranova project in Unity Editor
2. Wait for compilation to finish
3. Open a terminal in the project directory
4. Run `claude` to start Claude Code
5. Test: Ask Claude to "List all scenes in the project"

If Claude can see your Unity project, you're ready to go.

---

## Troubleshooting

### Unity won't open the project
- Make sure you installed the correct Unity version (check `ProjectSettings/ProjectVersion.txt`)
- Unity Hub → Installs → check if the right version is installed

### Claude Code can't connect to Unity
- Is Unity Editor running? (It must be open)
- Is the Coplay plugin active? (Window → Package Manager → verify Coplay is installed)
- Try: `claude mcp remove coplay-mcp` and re-add it

### Python/uvx not found
- Ensure Python ≥ 3.11 is in your PATH
- On Windows, you may need to restart the terminal after installing Python
- Install uvx manually if needed: `pip install uv`

### Windows-specific: MCP server connection fails
On native Windows (not WSL), you may need the `cmd /c` wrapper:
```bash
claude mcp add --scope user --transport stdio coplay-mcp \
  --env MCP_TOOL_TIMEOUT=720000 \
  -- cmd /c uvx --python ">=3.11" coplay-mcp-server@latest
```

---

## Optional: Recommended VS Code Extensions

If using VS Code or Cursor as a secondary editor:

- C# Dev Kit (Microsoft)
- Unity (Microsoft)
- GitLens
