using System.Threading.Tasks;

public interface IIntentAgent
{
    Task<UserIntent> InterpretAsync(string input, ConversationContext context);
}
