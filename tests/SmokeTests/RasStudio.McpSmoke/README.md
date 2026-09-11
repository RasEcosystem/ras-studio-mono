# Embedded MCP smoke test

`run-smoke.sh` starts RasStudio without Electron on an ephemeral loopback port, checks that `/mcp` rejects
unauthenticated requests, and connects with the official .NET MCP client. It discovers all read-only RasStudio tools,
validates their safety annotations, and invokes each tool against a clean local profile.

Run it from the repository root:

```bash
tests/SmokeTests/RasStudio.McpSmoke/run-smoke.sh
```
