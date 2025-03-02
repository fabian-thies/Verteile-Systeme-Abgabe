// File: ChatFileApp/Hubs/ChatHub.cs
using System.Security.Claims;
using ChatFileApp.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatFileApp.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendMessage(int chatId, string messageContent)
        {
            // Get current user from SignalR context
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return;

            // Create and save the new message
            var message = new Message
            {
                Content = messageContent,
                ConversationId = chatId,
                SentAt = DateTime.UtcNow,
                UserId = userId
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Load user information to include in the broadcast
            var savedMessage = await _context.Messages
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == message.Id);

            // Broadcast the new message to all clients in the chat group
            await Clients.Group(chatId.ToString()).SendAsync("ReceiveMessage", chatId, savedMessage?.User?.UserName, savedMessage?.SentAt, savedMessage?.Content);
        }

        public override async Task OnConnectedAsync()
        {
            // Add the connection to the chat group if a chatId is provided as a query string
            var chatId = Context.GetHttpContext()?.Request.Query["chatId"].ToString();
            if (!string.IsNullOrEmpty(chatId))
                await Groups.AddToGroupAsync(Context.ConnectionId, chatId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Remove the connection from the chat group based on the query string
            var chatId = Context.GetHttpContext()?.Request.Query["chatId"].ToString();
            if (!string.IsNullOrEmpty(chatId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);

            await base.OnDisconnectedAsync(exception);
        }
    }
}