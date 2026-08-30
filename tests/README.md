# Tests

- `UnitTests/RasStudio.Application.UnitTests` verifies assistant conversation and protocol history.
- `UnitTests/RasStudio.Infrastructure.UnitTests` verifies Ollama/OpenAI endpoint and model configuration.
- `UnitTests/RasStudio.Web.UnitTests` verifies MCP bearer authentication and safe Markdown rendering.
- `IntegrationTests/RasStudio.Web.IntegrationTests` starts the Web host in memory and verifies the protected embedded MCP endpoint.
- `SmokeTests/RasStudio.McpSmoke`, `SmokeTests/Desktop`, and `SmokeTests/Visual`
  contain executable end-to-end checks.

Run the .NET test projects with:

```bash
make dotnet-tests
```

Run the complete verification suite with:

```bash
make test
```
