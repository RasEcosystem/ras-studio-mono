using Microsoft.Extensions.AI;

namespace RasStudio.Application.Assistant;

public sealed class AssistantConversationStore
{
    private readonly List<ChatMessage> _chatHistory = [];
    private readonly List<AssistantConversationMessage> _messages = [];

    public IReadOnlyList<AssistantConversationMessage> Messages => _messages;

    public AssistantConversationMessage AddUser(string text)
    {
        var chatMessage = new ChatMessage(ChatRole.User, text);
        var message = new AssistantConversationMessage(
            AssistantMessageRole.User,
            text,
            chatMessage);

        _chatHistory.Add(chatMessage);
        _messages.Add(message);

        return message;
    }

    public AssistantConversationMessage AddAssistant(string text)
    {
        var message = new AssistantConversationMessage(
            AssistantMessageRole.Assistant,
            text);

        _messages.Add(message);
        return message;
    }

    public IReadOnlyList<ChatMessage> CreateChatHistorySnapshot()
    {
        return _chatHistory.ToArray();
    }

    public void AppendResponse(IEnumerable<ChatResponseUpdate> updates)
    {
        _chatHistory.AddMessages(updates);
    }

    public bool Remove(AssistantConversationMessage message)
    {
        if (!_messages.Remove(message)) return false;

        if (message.ChatMessage is not null) _chatHistory.Remove(message.ChatMessage);

        return true;
    }

    public void Clear()
    {
        _chatHistory.Clear();
        _messages.Clear();
    }
}

public sealed class AssistantConversationMessage(
    AssistantMessageRole role,
    string text,
    ChatMessage? chatMessage = null)
{
    internal ChatMessage? ChatMessage { get; } = chatMessage;

    public AssistantMessageRole Role { get; } = role;

    public string Text { get; set; } = text;
}

public enum AssistantMessageRole
{
    User,
    Assistant
}
