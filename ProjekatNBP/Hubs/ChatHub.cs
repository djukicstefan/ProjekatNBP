using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using ProjekatNBP.Session;
using ProjekatNBP.Models;
using Newtonsoft.Json;
using System;

namespace ProjekatNBP.Hubs
{
    public class ChatHub : Hub
    {
        public int? UserId => Context.GetHttpContext().Session.GetInt32(SessionKeys.UserId);
        public string UserName => Context.GetHttpContext().Session.GetString(SessionKeys.Username);

        public async Task SendMessage(string toRoom, string message)
        {
            var msg = new Message(UserId.Value, UserName, message, (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds);

            await Clients.Group(toRoom).SendAsync("MessageReceived", JsonConvert.SerializeObject(msg), toRoom);

            new Room(toRoom).SendMessage(msg);
        }

        public async Task Subscribe(string room) => await Groups.AddToGroupAsync(Context.ConnectionId, room);
    }
}
