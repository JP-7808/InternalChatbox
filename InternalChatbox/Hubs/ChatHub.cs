using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace InternalChatbox.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendPrivateMessage(int senderId, int receiverId, string message, string senderName)
        {
            // Send to the specific recipient
            await Clients.User(receiverId.ToString()).SendAsync("ReceivePrivateMessage", senderId, senderName, message);

            // Also send back to sender (so it appears in their chat window)
            await Clients.Caller.SendAsync("ReceivePrivateMessage", senderId, senderName, message);
        }

        public async Task SendGroupMessage(string groupId, int senderId, string message, string senderName)
        {
            await Clients.Group(groupId).SendAsync("ReceiveGroupMessage", groupId, senderId, senderName, message);
        }

        public async Task JoinGroup(string groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        }

        public async Task LeaveGroup(string groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
        }
    }
}