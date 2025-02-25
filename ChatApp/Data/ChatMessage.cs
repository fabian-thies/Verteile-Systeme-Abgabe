namespace ChatApp.Data;

public class ChatMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Text { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Sender navigation property.
    public User User { get; set; }
    
    // Conversation identifier.
    public int ConversationId { get; set; }
    
    // Navigation property for conversation.
    public Conversation Conversation { get; set; }
}