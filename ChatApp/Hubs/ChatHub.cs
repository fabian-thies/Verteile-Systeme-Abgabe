using ChatApp.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ChatAppContext _context;

        public ChatHub(ChatAppContext context)
        {
            _context = context;
        }

        public async Task SendMessage(User user, string message)
        {
            var userObject = await _context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
    
            if (userObject != null)
            {
                var chatMessage = new ChatMessage
                {
                    User = userObject,
                    Text = message,
                    Timestamp = DateTime.UtcNow
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                await Clients.All.SendAsync("ReceiveMessage", userObject, message);
            }
        }
    }
}