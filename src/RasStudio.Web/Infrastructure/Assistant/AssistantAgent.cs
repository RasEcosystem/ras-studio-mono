using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace RasStudio.Web.Infrastructure.Assistant;

public sealed class AssistantAgent(
    IChatClient chatClient,
    RasStudioMcpClient mcpClient)
{
    private const string SystemPrompt = """
                                        You are the RasStudio assistant. The available tools currently expose RasStudio application metadata only. Use them for questions about the application's identity, version, or description. Do not claim access to live RAS infrastructure data. Respond in the user's language and use Markdown when it improves readability.
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
