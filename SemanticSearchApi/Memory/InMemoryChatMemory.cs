using System.Collections.Generic;

public class InMemoryChatMemory : IChatMemory
{
    private static readonly Dictionary<string, ConversationContext> Store = new();
    private ConversationContext _context = new();

    public void Load(string sessionId)
    {
        Store.TryGetValue(sessionId, out _context);
        _context ??= new ConversationContext();
    }

    public void Save(string sessionId)
    {
        Store[sessionId] = _context;
    }

    public ConversationContext GetContext()
    {
        return _context;
    }

    public void UpdateContext(string userInput, string systemReply)
    {
        _context.History.Add(new MessagePair(userInput, systemReply));
    }
}

public class ConversationContext
{
    public List<MessagePair> History { get; set; } = new();
}

public record MessagePair(string User, string Bot);
