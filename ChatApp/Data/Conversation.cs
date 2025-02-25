namespace ChatApp.Data;

public class Conversation
{
    public int Id { get; set; }

    // For private conversations Name can be null.
    public string Name { get; set; }

    // Indicates whether this conversation is a group chat.
    public bool IsGroup { get; set; }

    // Collection of users participating in the conversation.
    public ICollection<User> Users { get; set; } = new List<User>();

    // Chat messages that belong to this conversation.
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}