using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Neo4j.Driver;
using System.Linq;

namespace ProjekatNBP.Hubs {
    public class AdsHub : NBPHub
    {
        public IDriver Driver => Startup.driver;

        public override async Task OnConnectedAsync()
        {
            if (!UserId.HasValue) return;

            IAsyncSession session = Driver.AsyncSession();
            try
            {
                var result = await session.RunAsync($"MATCH p=(u1:User)-[r:FOLLOW]->(u2:User) WHERE id(u1)={UserId} RETURN id(u2)");
                await Task.WhenAll((await result.ToListAsync()).Select(x => Subscribe(x["id(u2)"].As<string>())));
            }
            finally { await session.CloseAsync(); }
        }

        public async Task AdPosted(string adName)
        {
            IAsyncSession session = Driver.AsyncSession();
            try
            {
                var result = await session.RunAsync($"MATCH p=(u1:User)-[r:FOLLOW]->(u2:User) WHERE id(u2)={UserId} RETURN id(u1)");
                (await result.ToListAsync()).ForEach(x => RedisManager<string>.Push($"users:{x["id(u1)"].As<string>()}:notifications", $"{UserName} je postavio novi oglas - {adName}"));
            }
            finally { await session.CloseAsync(); }

            await Clients.Group(UserId.Value.ToString()).SendAsync("AdPosted", adName, UserName);
        }

        public async Task Subscribe(string room) => await Groups.AddToGroupAsync(Context.ConnectionId, room);
    }
}
