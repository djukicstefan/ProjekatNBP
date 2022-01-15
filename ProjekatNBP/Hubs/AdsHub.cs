using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using ProjekatNBP.Session;
using ProjekatNBP.Models;
using Newtonsoft.Json;
using System;
using System.Text;
using Neo4j.Driver;
using System.Linq;

namespace ProjekatNBP.Hubs
{
    public class AdsHub : NBPHub
    {
        public IDriver Driver => Startup.driver;

        public override async Task OnConnectedAsync()
        {
            if (!UserId.HasValue) return;

            IResultCursor result;
            IAsyncSession session = Driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH p=(u1:User)-[r:FOLLOW]->(u2:User) WHERE id(u1)={UserId} RETURN id(u2)");
                var idList = await result.ToListAsync();
                await Task.WhenAll(idList.Select(x => Subscribe(x["id(u2)"].As<string>())));
            }
            finally { await session.CloseAsync(); }
        }

        public async Task AdPosted(string adName)
        {
            IResultCursor result;
            IAsyncSession session = Driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH p=(u1:User)-[r:FOLLOW]->(u2:User) WHERE id(u2)={UserId} RETURN id(u1)");
                var idList = await result.ToListAsync();
                idList.ForEach(x => RedisManager<string>.Push($"users:{x["id(u1)"].As<string>()}:notifications", $"{UserName} je postavio novi oglas - {adName}"));
            }
            finally { await session.CloseAsync(); }

            await Clients.Group(UserId.Value.ToString()).SendAsync("AdPosted", adName, UserName);
        }

        public async Task Subscribe(string room) => await Groups.AddToGroupAsync(Context.ConnectionId, room);
    }
}
