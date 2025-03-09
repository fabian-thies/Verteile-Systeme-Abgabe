namespace Shared.Models;

public class MessageDto
{
    public string SenderName { get; set; }
    public int ReceiverId { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
}