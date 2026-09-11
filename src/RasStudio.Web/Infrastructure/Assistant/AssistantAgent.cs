using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace RasStudio.Web.Infrastructure.Assistant;

public sealed class AssistantAgent(
    IChatClient chatClient,
    RasStudioMcpClient mcpClient)
{
    private const string SystemPrompt = """
                                        You are the RasStudio assistant. Use get_rasstudio_info for application identity, get_rashub_status for RasHub connectivity and compatibility, get_infrastructure_overview for a broad RasHub/RasGate/RAS endpoint inventory, and get_application_issues for recent application warnings or errors. When asked whether RasGates are online, healthy, or having problems, call get_rasgate_status and clearly name any affected active gates. RasGate health is the last status persisted by RasHub, not a live refresh; include observation times when freshness matters. Do not claim access to RAS infrastructure data beyond what the tools return. Respond in the user's language and use Markdown when it improves readability.
                                        """;

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tools = await mcpClient.GetToolsAsync(cancellationToken);
        var messages = new List<ChatMessage>(history.Count + 1) { new(ChatRole.System, SystemPrompt) };

        messages.AddRange(history);

        var options = new ChatOptions { Tools = [.. tools] };

        await foreach (var update in chatClient.GetStreamingResponseAsync(
                           messages,
                           options,
                           cancellationToken))
            yield return update;
    }
}
