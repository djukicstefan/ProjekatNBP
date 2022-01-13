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
        private static readonly Dictionary<int, HashSet<string>> _connections = new();

        public int? UserId => Context.GetHttpContext().Session.GetInt32(SessionKeys.UserId);
        public string UserName => Context.GetHttpContext().Session.GetString(SessionKeys.Username);

        public async Task SendMessage(int to, string toRoom, string message)
        {
            var msg = new Message(UserName, message, DateTime.Now.Ticks);
            if (_connections.TryGetValue(to, out var conns))
            {
                await Clients.Clients(conns).SendAsync("MessageReceived", JsonConvert.SerializeObject(msg), toRoom);
                msg = msg with { Read = true };
            }
            
            RedisManager<Message>.Push($"user:{to}:room:{toRoom}", msg);
        }

        public override async Task OnConnectedAsync()
        {
            if (UserId.HasValue)
            {
                _connections.TryAdd(UserId.Value, new());
                _connections[UserId.Value].Add(Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (UserId.HasValue && _connections.ContainsKey(UserId.Value))
            {
                _connections[UserId.Value].Remove(Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }
    }
}
