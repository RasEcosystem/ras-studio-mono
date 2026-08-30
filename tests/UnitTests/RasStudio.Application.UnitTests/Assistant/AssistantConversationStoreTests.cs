using Microsoft.Extensions.AI;
using RasStudio.Application.Assistant;

namespace RasStudio.Application.UnitTests.Assistant;

public sealed class AssistantConversationStoreTests
{
    [Fact]
    public void AddAndRemoveUserKeepUiAndProtocolHistoryInSync()
    {
        var store = new AssistantConversationStore();

        var message = store.AddUser("Show the current RasStudio version");

        var uiMessage = Assert.Single(store.Messages);
        Assert.Same(message, uiMessage);
        Assert.Equal(AssistantMessageRole.User, uiMessage.Role);
        Assert.Equal("Show the current RasStudio version", uiMessage.Text);

        var protocolMessage = Assert.Single(store.CreateChatHistorySnapshot());
        Assert.Equal(ChatRole.User, protocolMessage.Role);
        Assert.Equal("Show the current RasStudio version", protocolMessage.Text);

        Assert.True(store.Remove(message));
        Assert.Empty(store.Messages);
        Assert.Empty(store.CreateChatHistorySnapshot());
    }

    [Fact]
    public void AppendResponsePreservesFunctionCallsAndResults()
    {
        var store = new AssistantConversationStore();
        store.AddUser("What is this application?");

        ChatResponseUpdate[] updates =
        [
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        "call-1",
                        "get_rasstudio_info",
                        new Dictionary<string, object?>())
                ]),
            new(
                ChatRole.Tool,
                [new FunctionResultContent("call-1", new { name = "RasStudio Mono" })]),
            new(ChatRole.Assistant, "RasStudio Mono manages RAS infrastructure.")
        ];

        store.AppendResponse(updates);

        var history = store.CreateChatHistorySnapshot();
        var contents = history.SelectMany(message => message.Contents).ToArray();

        Assert.Contains(contents,
            content => content is FunctionCallContent call &&
                       call.Name == "get_rasstudio_info");
        Assert.Contains(contents, content => content is FunctionResultContent);
        Assert.Equal(
            "RasStudio Mono manages RAS infrastructure.",
            history.Last().Text);
    }

    [Fact]
    public void ClearRemovesUiAndProtocolHistory()
    {
        var store = new AssistantConversationStore();
        store.AddUser("Hello");
        store.AddAssistant("Hi");
        store.AppendResponse([new ChatResponseUpdate(ChatRole.Assistant, "Hi")]);

        store.Clear();

        Assert.Empty(store.Messages);
        Assert.Empty(store.CreateChatHistorySnapshot());
    }
}
