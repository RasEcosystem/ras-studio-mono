# Tests

- `UnitTests/RasStudio.Application.UnitTests` verifies assistant conversation, protocol history, and RasHub models.
- `UnitTests/RasStudio.Infrastructure.UnitTests` verifies Ollama/OpenAI configuration,
  protected RasHub settings, HTTP transport, and Gate, endpoint, cluster, and infobase clients.
- `UnitTests/RasStudio.Web.UnitTests` verifies catalog pagination, list reload and search
  behavior, cluster update commands, diagnostics, MCP tools, and safe Markdown rendering.
- `IntegrationTests/RasStudio.Web.IntegrationTests` starts the Web host in memory and
  verifies routes, service composition, the protected MCP endpoint, and table footer rendering.
- `SmokeTests/RasStudio.McpSmoke` and `SmokeTests/Desktop` contain executable
  end-to-end checks.

Run the .NET test projects with:

```bash
make dotnet-tests
```

Run the complete verification suite with:

```bash
make test
```
