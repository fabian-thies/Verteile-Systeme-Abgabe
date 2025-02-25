namespace ChatApp.Data;

public class User
{
    public int Id { get; set; }
    
    public string Username { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Chat messages sent by the user.
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    
    // Conversations in which the user participates.
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}