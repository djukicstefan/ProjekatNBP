using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using ProjekatNBP.Session;

namespace ProjekatNBP.Hubs
{
    public class NBPHub : Hub
    {
        public int? UserId => Context.GetHttpContext().Session.GetInt32(SessionKeys.UserId);
        public string UserName => Context.GetHttpContext().Session.GetString(SessionKeys.Username);
    }
}
