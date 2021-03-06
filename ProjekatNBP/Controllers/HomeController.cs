using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ProjekatNBP.Extensions;
using ProjekatNBP.Session;
using System.Diagnostics;
using ProjekatNBP.Models;
using Neo4j.Driver;
using System.Linq;
using System.Text;
using System;

namespace ProjekatNBP.Controllers {
    public class HomeController : Controller {
        private readonly ILogger<HomeController> _logger;
        private readonly IDriver _driver;

        public (UserInfo, int)[] Leaderboard => RedisManager<UserInfo>.GetSortedSet("leaderboard", 10);

        public HomeController(ILogger<HomeController> logger, IDriver driver) {
            _logger = logger;
            _driver = driver;
        }

        public async Task<IActionResult> Index() {
            List<Category> categoryList;
            List<Ad> adList = null;
            List<Ad> adRecomendList = null;

            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try {
                result = await session.RunAsync($"MATCH (c:Category) RETURN c");
                
                categoryList = (await result.ToListAsync()).Select(cat => {
                    INode category = cat["c"].As<INode>();
                    return new Category {
                        Id = (int)category.Id,
                        Name = category.Properties["name"].ToString()
                    };
                }).ToList();

                await Task.WhenAll(categoryList.Select(async x => {
                    session = _driver.AsyncSession();
                    result = await session.RunAsync($"MATCH (c:Category {{name: '{x.Name}'}})-[r]-(ad:Ad) return ad");
                    var relationships = await result.ToListAsync();
                    relationships.ForEach(rel => {
                        INode rel1 = rel["ad"].As<INode>();
                        var ad = new Ad {
                            Id = (int)rel1.Id,
                            Name = rel1.Properties["name"].ToString(),
                            Category = rel1.Properties["category"].ToString(),
                            Price = rel1.Properties["price"].ToString(),
                            Description = rel1.Properties["description"].ToString()
                        };
                        x.Ads.Add(ad);
                    });
                }));

                var userId = HttpContext.Session.GetUserId();
                if (userId >= 0) {
                    session = _driver.AsyncSession();
                    result = await session.RunAsync(@$"MATCH (u:User)-[r:VISITED]-(ad:Ad) 
                                    WHERE id(u)={userId} 
                                    RETURN ad, r, u");

                    adList = (await result.ToListAsync()).Select(a => {
                        INode aa = a["ad"].As<INode>();
                        return new Ad {
                            Id = (int)aa.Id,
                            Name = aa.Properties["name"].ToString(),
                            Category = aa.Properties["category"].ToString(),
                            Price = aa.Properties["price"].ToString(),
                            Description = aa.Properties["description"].ToString()
                        };
                    }).ToList();

                    session = _driver.AsyncSession();
                    result = await session.RunAsync(@$"MATCH (u:User)-[r:VISITED]-(ad:Ad) 
                                    WHERE id(u)={userId} 
                                    RETURN ad.category
                                    ORDER BY r.visitedCount DESC
                                    LIMIT 1");
                    var fav = await result.ToListAsync();
                    if (fav.Count > 0) {
                        var favCategory = fav[0].Values.Values.FirstOrDefault().ToString();

                        session = _driver.AsyncSession();
                        result = await session.RunAsync(@$"MATCH (c:Category)-[r:HAS]-(ad:Ad) 
                                    WHERE c.name = '{favCategory}' 
                                    RETURN ad");

                        adRecomendList = (await result.ToListAsync()).Select(a => {
                            INode aa = a["ad"].As<INode>();

                            Ad ad = new() {
                                Id = (int)aa.Id,
                                Name = aa.Properties["name"].ToString(),
                                Category = aa.Properties["category"].ToString(),
                                Price = aa.Properties["price"].ToString(),
                                Description = aa.Properties["description"].ToString()
                            };

                            return ad;
                        }).Where(x => adList.Find(y => x.Id == y.Id) == null).ToList();
                    }
                }
            }
            finally {
                await session.CloseAsync();
            }

            return View(new Ads { CategoryList = categoryList, AdList = adList, AdRecomendList = adRecomendList, Leaderboard = Leaderboard });
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() 
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        public IActionResult Register() {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index");

            return View();
        }

        public IActionResult Login() {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index");

            return View();
        }

        public async Task<IActionResult> SavedAds() {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            var userId = HttpContext.Session.GetUserId();
            IAsyncSession session = _driver.AsyncSession();
            try {
                var result = await session.RunAsync($"MATCH (u:User)-[r:SAVED]-(ad:Ad) WHERE id(u) = {userId} RETURN ad");
                var ads = await result.ToListAsync();
                return View(ads.Select(a => {
                    INode ad1 = a["ad"].As<INode>();

                    return new Ad {
                        Id = (int)ad1.Id,
                        Name = ad1.Properties["name"].ToString(),
                        Category = ad1.Properties["category"].ToString(),
                        Price = ad1.Properties["price"].ToString(),
                        Description = ad1.Properties["description"].ToString()
                    };
                }).ToList());
            }
            finally {
                await session.CloseAsync();
            }
        }

        public async Task<IActionResult> Profile() {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            var userId = HttpContext.Session.GetUserId();
            var session = _driver.AsyncSession();
            try {
                var result = await session.RunAsync($"MATCH (u:User) WHERE id(u) = {userId} RETURN u");
                var user = (await result.SingleAsync())["u"].As<INode>();
                return View(new User {
                    Id = userId,
                    Username = user.Properties["username"].ToString(),
                    Password = user.Properties["password"].ToString(),
                    City = user.Properties["city"].ToString(),
                    Email = user.Properties["email"].ToString(),
                    Phone = user.Properties["phone"].ToString()
                });
            }
            finally {
                await session.CloseAsync();
            }
        }

        public async Task<IActionResult> MineAds() {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            var userId = HttpContext.Session.GetUserId();
            List<Ad> adList = new();
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try {
                result = await session.RunAsync($"MATCH (u:User)-[r:POSTED]-(ad:Ad) WHERE id(u) = {userId} RETURN ad");
                var ads = await result.ToListAsync();
                ads.ForEach(a => {
                    INode ad1 = a["ad"].As<INode>();
                    adList.Add(new() {
                        Id = (int)ad1.Id,
                        Name = ad1.Properties["name"].ToString(),
                        Category = ad1.Properties["category"].ToString(),
                        Price = ad1.Properties["price"].ToString(),
                        Description = ad1.Properties["description"].ToString()
                    });
                });
            }
            finally {
                await session.CloseAsync();
            }

            return View(adList);
        }

        public async Task<IActionResult> AdView(int adId) {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            var userId = HttpContext.Session.GetUserId();
            Ad ad;
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try {
                result = await session.RunAsync($"MATCH (u:User)-[r:POSTED]-(ad:Ad) WHERE id(ad) = {adId} RETURN u, ad");
                var ad1 = await result.SingleAsync();

                INode ad2 = ad1["ad"].As<INode>();
                INode u = ad1["u"].As<INode>();

                if (userId == (int)u.Id) {
                    ad = new() {
                        Id = (int)ad2.Id,
                        Name = ad2.Properties["name"].ToString(),
                        Category = ad2.Properties["category"].ToString(),
                        Price = ad2.Properties["price"].ToString(),
                        Description = ad2.Properties["description"].ToString(),
                        User = null
                    };
                } else {
                    session = _driver.AsyncSession();
                    result = await session.RunAsync($"MATCH (u1:User)<-[r:FOLLOW]-(u2:User) WHERE id(u1) = {(int)u.Id} RETURN id(u2)");
                    var followersId = await result.ToListAsync();

                    User us = new() {
                        Id = (int)u.Id,
                        Username = u.Properties["username"].ToString(),
                        Followers = followersId.Select(x => x["id(u2)"].As<int>()).ToList()
                    };

                    ad = new() {
                        Id = (int)ad2.Id,
                        Name = ad2.Properties["name"].ToString(),
                        Category = ad2.Properties["category"].ToString(),
                        Price = ad2.Properties["price"].ToString(),
                        Description = ad2.Properties["description"].ToString(),
                        User = us
                    };
                }


                session = _driver.AsyncSession();
                result = await session.RunAsync(@$"MATCH (u:User)-[r:VISITED]-(ad:Ad) 
                                    WHERE id(u)={userId} 
                                    RETURN ad, r, u");

                var visitedAds = await result.ToListAsync();

                var rel = visitedAds.Select(rec => rec["r"].As<IRelationship>()).ToList().Find(x => x.EndNodeId == adId);
                long count = 1;
                if (rel != null && rel.Properties.TryGetValue("visitedCount", out object val)) {
                    count = (long)val + 1;
                }


                if (visitedAds.Count < 6) {
                    session = _driver.AsyncSession();
                    if (rel == null) {
                        result = await session.RunAsync(@$"MATCH(u:User) WHERE id(u)={userId} 
                                                MATCH (ad:Ad) WHERE id(ad)={adId} 
                                                CREATE (u)-[:VISITED {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}]->(ad)");
                    } else {
                        result = await session.RunAsync(@$"MATCH(u:User)-[r:VISITED]-(ad:Ad) 
                                                WHERE id(u)={userId} AND id(ad)={adId} 
                                                SET r = {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}");
                    }
                } else {
                    var min = visitedAds.Min(x => (long)x["r"].As<IRelationship>().Properties["timestamp"]);
                    session = _driver.AsyncSession();
                    result = await session.RunAsync(@$"MATCH(u:User)-[r:VISITED]->(ad:Ad) 
                                            WHERE id(u)={userId} AND r.timestamp={min} 
                                            DELETE r");

                    session = _driver.AsyncSession();
                    if (rel == null) {
                        await session.RunAsync(@$"MATCH(u:User) WHERE id(u)={userId} 
                                                MATCH (ad:Ad) WHERE id(ad)={adId} 
                                                CREATE (u)-[:VISITED {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}]->(ad)");
                    } else {
                        await session.RunAsync(@$"MATCH(u:User)-[r:VISITED]-(ad:Ad) 
                                                WHERE id(u)={userId} AND id(ad)={adId} 
                                                SET r = {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}");
                    }
                }
            }
            finally {
                await session.CloseAsync();
            }

            return View(ad);
        }

        public async Task<IActionResult> ChangeAd(string id, string name, string category, string price, string descritpion) {
            var adId = int.Parse(id);
            IAsyncSession session = _driver.AsyncSession();
            try {
                _ = await session.RunAsync(@$"MATCH (ad:Ad) 
                                    WHERE id(ad)={adId}
                                    SET ad = {{ name: '{name}', category: '{category}', price: '{price}', description: '{descritpion}' }} ");
            }
            finally {
                await session.CloseAsync();
            }

            return RedirectToAction("MineAds", "Home");
        }

        public async Task<IActionResult> FollowUser(int uId) {
            var userId = HttpContext.Session.GetUserId();
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try {
                result = await session.RunAsync(@$"MATCH (u1:User)-[r:FOLLOW]->(u2:User)
                                        WHERE id(u1) = {userId} AND id(u2) = {uId}
                                        RETURN u1, r, u2");
                var checkIfFollow = await result.ToListAsync();

                if (checkIfFollow.Count == 0) {
                    session = _driver.AsyncSession();
                    _ = await session.RunAsync(@$"MATCH(u1:User) WHERE id(u1)={userId} 
                                    MATCH (u2:User) WHERE id(u2)={uId} 
                                    CREATE (u1)-[:FOLLOW]->(u2)");

                    RedisManager<int>.Push($"users:{uId}:followers", userId);
                }
            }
            finally {
                await session.CloseAsync();
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> UnfollowUser(int uId) {
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            var userId = HttpContext.Session.GetUserId();
            IAsyncSession session = _driver.AsyncSession();
            try {
                _ = await session.RunAsync(@$"MATCH (u1:User)-[r:FOLLOW]->(u2:User)
                                        WHERE id(u1) = {userId} AND id(u2) = {uId}
                                        DELETE r");
            }
            finally {
                await session.CloseAsync();
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult RemoveNotification(string path, string item)
            => RedisManager<string>.DeleteItem(path, item) ? Ok() : BadRequest();

        public async Task<IActionResult> UserAds(int idUser)
        {
            List<Ad> adList = null;
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {                
                result = await session.RunAsync($"MATCH (n:User)-[r:POSTED]->(ad:Ad) WHERE id(n) = {idUser} RETURN ad");
                var list = await result.ToListAsync();

                if(list.Count > 0)
                {
                    User u = new User { Id = idUser };
                    adList = new List<Ad>();
                    list.ForEach(x => {
                        INode ad = x["ad"].As<INode>();

                        adList.Add(new Ad
                        {
                            Id = (int)ad.Id,
                            Name = ad.Properties["name"].ToString(),
                            Category = ad.Properties["category"].ToString(),
                            Price = ad.Properties["price"].ToString(),
                            Description = ad.Properties["description"].ToString(),
                            User = u
                        });
                    });
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(adList);
        }
    }
}
