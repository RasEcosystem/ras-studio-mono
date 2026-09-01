# Embedded MCP smoke test

`run-smoke.sh` starts RasStudio without Electron on an ephemeral loopback port, checks that `/mcp` rejects
unauthenticated requests, connects with the official .NET MCP client, discovers `get_rasstudio_info`, validates its
safety annotations, and invokes it.

Run it from the repository root:

```bash
tests/SmokeTests/RasStudio.McpSmoke/run-smoke.sh
```
