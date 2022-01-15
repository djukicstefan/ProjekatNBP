using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ProjekatNBP.Extensions;
using StackExchange.Redis;
using ProjekatNBP.Session;
using ProjekatNBP.Models;
using ProjekatNBP.Hubs;
using Neo4j.Driver;
using System.Linq;
using System;

namespace ProjekatNBP.Controllers {
    public class UserController : Controller {
        private readonly IDriver _driver;
        private readonly IConnectionMultiplexer _redis;
        private readonly IHubContext<AdsHub> _hub;

        private static readonly object _lock = new();
        private static bool _firstTimeRun = true;

        public UserController(IDriver driver, IHubContext<AdsHub> hub, IConnectionMultiplexer redis) {
            _driver = driver;
            _hub = hub;
            _redis = redis;

            lock (_lock) {
                if (!_firstTimeRun)
                    return;
                _firstTimeRun = false;

                RedisManager<AdNotification>.Subscribe("AdPostedNotification", async (channel, data) => {
                    data.followers.ToList().ForEach(x => RedisManager<string>
                        .Push($"users:{x}:notifications", $"{data.userName} je postavio novi oglas - {data.adName}|{data.adId}"));
                    await hub.Clients.All.SendAsync("NotificationReceived", data);
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password) {
            if (HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            var session = _driver.AsyncSession();
            try {
                var result = await session.RunAsync($"MATCH (u:User {{username: '{username}', password: '{password}'}}) RETURN id(u)");

                var res = await result.ToListAsync();
                if (res.Count == 0)
                    return RedirectToAction("Login", "Home");

                var userId = res[0]["id(u)"].As<int>();

                if (userId != -1) {
                    HttpContext.Session.SetString(SessionKeys.Username, username);
                    HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
                    return RedirectToAction("Index", "Home");
                }
            }
            finally {
                await session.CloseAsync();
            }

            return RedirectToAction("Login", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string city, string phone, string password) {
            if (HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Index", "Home");

            var session = _driver.AsyncSession();
            try {
                var result = await session.RunAsync($"CREATE (user:User {{username: '{username}', password:  '{password}', phone: '{phone}', email:  '{email}', city: '{city}' }}) return id(user)");
                var userId = await result.SingleAsync(record => record["id(user)"].As<int>());
                if (userId != -1) {
                    HttpContext.Session.SetString(SessionKeys.Username, username);
                    HttpContext.Session.SetInt32(SessionKeys.UserId, userId);
                    return RedirectToAction("Index", "Home");
                }
            }
            finally {
                await session.CloseAsync();
            }

            return RedirectToAction("Register", "Home");
        }

        public IActionResult Logout() {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> PlaceAd(string name, string category, string price, string description) {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            IResultCursor result;
            var session = _driver.AsyncSession();
            try {
                var res = await (await session.RunAsync($"MATCH (c:Category {{ name: '{category}' }}) RETURN id(c)")).ToListAsync();

                int categoryID = -1;
                if (res.Count == 0) {
                    result = await session.RunAsync($"CREATE (category:Category {{ name: '{category}' }}) return id(category)");
                    categoryID = await result.SingleAsync(record => record["id(category)"].As<int>());
                } else
                    categoryID = res[0]["id(c)"].As<int>();

                result = await session.RunAsync($"CREATE (ad:Ad {{ name: '{name}', category: '{category}', price: '{price}', description: '{description}' }}) return id(ad)");
                var adId = await result.SingleAsync(record => record["id(ad)"].As<int>());

                if (adId == -1)
                    return RedirectToAction("Index", "Home");

                session = _driver.AsyncSession();
                result = await session.WriteTransactionAsync(tx => tx.RunAsync(@$"MATCH(c:Category) WHERE id(c)={categoryID} 
                                    MATCH (ad:Ad) WHERE id(ad)={adId} 
                                    CREATE (c)-[:HAS]->(ad)"));

                var userId = HttpContext.Session.GetUserId();
                session = _driver.AsyncSession();
                result = await session.WriteTransactionAsync(tx => tx.RunAsync(@$"MATCH(u:User) WHERE id(u)={userId} 
                                    MATCH (ad:Ad) WHERE id(ad)={adId} 
                                    CREATE (u)-[:POSTED]->(ad)"));

                try {
                    result = await session.RunAsync($"MATCH p=(u1:User)-[r:FOLLOW]->(u2:User) WHERE id(u2)={userId} RETURN id(u1)");
                    var idList = await result.ToListAsync();
                    var ids = idList?.Select(x => x["id(u1)"].As<string>()).ToArray() ?? Array.Empty<string>();

                    var userName = HttpContext.Session.GetUsername();
                    RedisManager<AdNotification>.Publish("AdPostedNotification", new AdNotification(ids, adId, name, userId, userName));
                }
                finally { await session.CloseAsync(); }

            }
            finally {
                await session.CloseAsync();
            }


            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> SaveAd(int adId) {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            IAsyncSession session = _driver.AsyncSession();
            try {
                _ = await session.WriteTransactionAsync(tx => tx.RunAsync(@$"MATCH (u:User), (ad:Ad) 
                                        WHERE id(u)={HttpContext.Session.GetUserId()} AND id(ad)={adId}                                      
                                        CREATE (u)-[s:SAVED]->(ad)
                                        RETURN type(s)"));
            }
            finally {
                await session.CloseAsync();
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> DeleteAd(int adId) {
            IAsyncSession session = _driver.AsyncSession();
            try {
                _ = await session.RunAsync($"MATCH (n) WHERE id(n) = {adId} DETACH DELETE n");
            }
            finally {
                await session.CloseAsync();
            }
            return RedirectToAction("MineAds", "Home");
        }

        public async Task<IActionResult> ChangeData(string id, string username, string email, string city, string phone, string password1, string password, string repassword) {
            var newPass = password1;
            if (!string.IsNullOrEmpty(password) && password.CompareTo(repassword) == 0)
                    newPass = password;

            var userId = int.Parse(id);
            IAsyncSession session = _driver.AsyncSession();
            try {
                _ = await session.RunAsync(@$"MATCH (u:User) 
                                    WHERE id(u)={userId}
                                    SET u = {{ username: '{username}', password: '{newPass}', phone: '{phone}', email: '{email}', city: '{city}' }} ");
            }
            finally {
                await session.CloseAsync();
            }

            return RedirectToAction("Profile", "Home");
        }
    }
}
