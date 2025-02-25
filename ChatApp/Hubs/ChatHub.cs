using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        
        public async Task SendPrivateMessage(string user, string toUser, string message)
        {
            await Clients.User(toUser).SendAsync("ReceiveMessage", user, message);
        }
    }
}