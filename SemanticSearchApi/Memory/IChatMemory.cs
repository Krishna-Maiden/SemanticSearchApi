using System.Collections.Generic;

public interface IChatMemory
{
    void Load(string sessionId);
    void Save(string sessionId);
    ConversationContext GetContext();
    void UpdateContext(string userInput, string systemReply);
}