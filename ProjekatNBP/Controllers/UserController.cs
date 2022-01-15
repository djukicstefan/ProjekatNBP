using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Neo4j.Driver;
using ProjekatNBP.Extensions;
using ProjekatNBP.Hubs;
using ProjekatNBP.Models;
using ProjekatNBP.Session;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjekatNBP.Controllers
{
    public class UserController : Controller
    {
        private readonly IDriver _driver;
        private readonly IConnectionMultiplexer _redis;
        private readonly IHubContext<AdsHub> _hub;

        private static object _lock = new object();
        private static bool _firstTimeRun = true;
        public UserController(IDriver driver, IHubContext<AdsHub> hub, IConnectionMultiplexer redis)
        {
            _driver = driver;
            _hub = hub;
            _redis = redis;

            lock (_lock)
            {
                if (!_firstTimeRun) return;
                _firstTimeRun = false;

                RedisManager<AdNotification>.Subscribe("AdPostedNotification", async (channel, data) => {
                    data.followers.ToList().ForEach(x=> RedisManager<string>
                        .Push($"users:{x}:notifications", $"{data.userName} je postavio novi oglas - {data.adName}|{data.adId}"));
                    await hub.Clients.All.SendAsync("NotificationReceived", data);
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index", "Home");

            IResultCursor result;
            int userId = -1;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (u:User {{username: '{username}', password: '{password}'}}) RETURN id(u)");

                var res = await result.ToListAsync();
                if (res.Count == 0)
                    return RedirectToAction("Login", "Home");

                userId = res[0]["id(u)"].As<int>();

                if (userId != -1)
                {                    
                    HttpContext.Session.SetString(SessionKeys.Username, username);
                    HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
                    return RedirectToAction("Index", "Home");
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Login", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string city, string phone, string password)
        {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index", "Home");

            var statementText = new StringBuilder();
            statementText.Append($"CREATE (user:User {{username: '{username}', password:  '{password}', phone: '{phone}', email:  '{email}', city: '{city}' }}) return id(user)");

            IResultCursor result;
            int userId = -1;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync(statementText.ToString());
                userId = await result.SingleAsync(record => record["id(user)"].As<int>());
                if (userId != -1)
                {
                    HttpContext.Session.SetString(SessionKeys.Username, username);
                    HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
                    return RedirectToAction("Index", "Home");
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Register", "Home");
        }

        public IActionResult Logout()
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> PlaceAd(string name, string category, string price, string description)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            string userName = HttpContext.Session.GetString(SessionKeys.Username);
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            var statementText = new StringBuilder();
            IResultCursor result;
            int categoryID = -1;
            int adId = -1;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (c:Category {{ name: '{category}' }}) RETURN id(c)");

                var res = await result.ToListAsync();
                if (res.Count == 0)
                {
                    statementText.Append($"CREATE (category:Category {{ name: '{category}' }}) return id(category)");

                    result = await session.RunAsync(statementText.ToString());
                    categoryID = await result.SingleAsync(record => record["id(category)"].As<int>());
                    statementText.Clear();
                }
                else
                {
                    categoryID = res[0]["id(c)"].As<int>();
                }

                statementText.Append($"CREATE (ad:Ad {{ name: '{name}', category: '{category}', price: '{price}', description: '{description}' }}) return id(ad)");
                result = await session.RunAsync(statementText.ToString());
                adId = await result.SingleAsync(record => record["id(ad)"].As<int>());

                if(adId == -1)
                    return RedirectToAction("Index", "Home");

                statementText.Clear();
                statementText.Append(@$"MATCH(c:Category) WHERE id(c)={categoryID} 
                                    MATCH (ad:Ad) WHERE id(ad)={adId} 
                                    CREATE (c)-[:HAS]->(ad)");
                session = _driver.AsyncSession();
                result = await session.WriteTransactionAsync(tx => tx.RunAsync(statementText.ToString()));

                statementText.Clear();
                statementText.Append(@$"MATCH(u:User) WHERE id(u)={userId} 
                                    MATCH (ad:Ad) WHERE id(ad)={adId} 
                                    CREATE (u)-[:POSTED]->(ad)");
                session = _driver.AsyncSession();
                result = await session.WriteTransactionAsync(tx => tx.RunAsync(statementText.ToString()));

                try
                {
                    result = await session.RunAsync($"MATCH p=(u1:User)-[r:FOLLOW]->(u2:User) WHERE id(u2)={userId} RETURN id(u1)");
                    var idList = await result.ToListAsync();
                    var ids = idList?.Select(x => x["id(u1)"].As<string>()).ToArray() ?? new string[0];
                    
                    RedisManager<AdNotification>.Publish("AdPostedNotification", new AdNotification(ids, adId, name, userId, userName));
                }
                finally { await session.CloseAsync(); }

            }
            finally
            {
                await session.CloseAsync();
            }


            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> SaveAd(int adId)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            IResultCursor result;            
            IAsyncSession session = _driver.AsyncSession();
            var statementText = new StringBuilder();
            try
            {
                statementText.Append(@$"MATCH (u:User), (ad:Ad) 
                                        WHERE id(u)={userId} AND id(ad)={adId}                                      
                                        CREATE (u)-[s:SAVED]->(ad)
                                        RETURN type(s)");
                result = await session.WriteTransactionAsync(tx => tx.RunAsync(statementText.ToString()));

            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> DeleteAd(int adId)
        {
            IResultCursor result;            
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (n) WHERE id(n) = {adId} DETACH DELETE n");                                
            }
            finally
            {
                await session.CloseAsync();
            }
            return RedirectToAction("MineAds", "Home");
        }

        public async Task<IActionResult> ChangeData(string id, string username, string email, string city, string phone, string password1, string password, string repassword)
        {
            string newPass = password1;
            if(!string.IsNullOrEmpty(password))
            {
                if (password.CompareTo(repassword) == 0)
                    newPass = password;
            }

            int userId = int.Parse(id);
            var statementText = new StringBuilder();
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                statementText.Append(@$"MATCH (u:User) 
                                    WHERE id(u)={userId}
                                    SET u = {{ username: '{username}', password: '{newPass}', phone: '{phone}', email: '{email}', city: '{city}' }} ");

                result = await session.RunAsync(statementText.ToString());
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("Profile", "Home");
        }
        
    }
}
