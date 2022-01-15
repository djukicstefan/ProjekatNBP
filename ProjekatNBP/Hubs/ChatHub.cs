using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using ProjekatNBP.Models;
using Newtonsoft.Json;
using System;

namespace ProjekatNBP.Hubs {
    public class ChatHub : NBPHub
    {
        public async Task SendMessage(string toRoom, string message)
        {
            var msg = new Message(UserId.Value, UserName, message, (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds);
            await Clients.Group(toRoom).SendAsync("MessageReceived", JsonConvert.SerializeObject(msg), toRoom);
            Room.SendMessage(toRoom, msg);
        }

        public async Task Subscribe(string room) => await Groups.AddToGroupAsync(Context.ConnectionId, room);
    }
}
