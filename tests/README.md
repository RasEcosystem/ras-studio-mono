# Tests

- `UnitTests/RasStudio.Application.UnitTests` verifies assistant conversation, protocol history, and RasHub models.
- `UnitTests/RasStudio.Infrastructure.UnitTests` verifies Ollama/OpenAI configuration, protected RasHub settings, and the RasGate API client.
- `UnitTests/RasStudio.Web.UnitTests` verifies MCP bearer authentication and safe Markdown rendering.
- `IntegrationTests/RasStudio.Web.IntegrationTests` starts the Web host in memory and verifies the protected embedded MCP endpoint and RasGate service composition.
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
