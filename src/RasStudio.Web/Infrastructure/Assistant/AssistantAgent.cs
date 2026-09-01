using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace RasStudio.Web.Infrastructure.Assistant;

public sealed class AssistantAgent(
    IChatClient chatClient,
    RasStudioMcpClient mcpClient)
{
    private const string SystemPrompt = """
                                        You are the RasStudio assistant. Use the available tools whenever the user asks about the application or RAS infrastructure. Do not invent runtime data that a tool can provide. Respond in the user's language and use Markdown when it improves readability.
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
