using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using ProjekatNBP.Extensions;
using ProjekatNBP.Models;
using ProjekatNBP.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjekatNBP.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IDriver _driver;

        public HomeController(ILogger<HomeController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        public async Task<IActionResult> Index(string category)
        {
            List<Category> categoryList = new List<Category>();
            List<Ad> adList = null;
            List<Ad> adRecomendList = null;

            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (c:Category) RETURN c");
                var categories = await result.ToListAsync();
                categories.ForEach(cat =>
                {
                    INode category = cat["c"].As<INode>();
                    Category category1 = new Category
                    {
                        Id = (int)category.Id,
                        Name = category.Properties["name"].ToString()
                    };

                    categoryList.Add(category1);
                });

                for (int i = 0; i < categoryList.Count; i++)
                {
                    session = _driver.AsyncSession();
                    result = await session.RunAsync($"MATCH (c:Category {{name: '{categoryList[i].Name}'}})-[r]-(ad:Ad) return ad");
                    var relationships = await result.ToListAsync();
                    relationships.ForEach(rel =>
                    {
                        INode rel1 = rel["ad"].As<INode>();
                        Ad ad = new Ad
                        {
                            Id = (int)rel1.Id,
                            Name = rel1.Properties["name"].ToString(),
                            Category = rel1.Properties["category"].ToString(),
                            Price = rel1.Properties["price"].ToString(),
                            Description = rel1.Properties["description"].ToString()
                        };
                        categoryList[i].Ads.Add(ad);
                    });
                }


                int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
                if (userId > 0)
                {
                    adList = new List<Ad>();
                    StringBuilder statementText = new StringBuilder();
                    session = _driver.AsyncSession();
                    statementText.Append(@$"MATCH (u:User)-[r:VISITED]-(ad:Ad) 
                                    WHERE id(u)={userId} 
                                    RETURN ad, r, u");

                    result = await session.RunAsync(statementText.ToString());
                    var visitedAds = await result.ToListAsync();


                    visitedAds.ForEach(a =>
                    {
                        INode aa = a["ad"].As<INode>();

                        Ad ad = new Ad
                        {
                            Id = (int)aa.Id,
                            Name = aa.Properties["name"].ToString(),
                            Category = aa.Properties["category"].ToString(),
                            Price = aa.Properties["price"].ToString(),
                            Description = aa.Properties["description"].ToString()
                        };

                        adList.Add(ad);
                    });

                    statementText.Clear();
                    session = _driver.AsyncSession();
                    statementText.Append(@$"MATCH (u:User)-[r:VISITED]-(ad:Ad) 
                                    WHERE id(u)={userId} 
                                    RETURN ad.category
                                    ORDER BY r.visitedCount DESC
                                    LIMIT 1");

                    result = await session.RunAsync(statementText.ToString());
                    var fav = await result.ToListAsync();
                    if (fav.Count > 0)
                    {
                        string favCategory = fav[0].Values.Values.FirstOrDefault().ToString();

                        statementText.Clear();
                        session = _driver.AsyncSession();
                        statementText.Append(@$"MATCH (c:Category)-[r:HAS]-(ad:Ad) 
                                    WHERE c.name = '{favCategory}' 
                                    RETURN ad                                    
                                    LIMIT 3");

                        result = await session.RunAsync(statementText.ToString());
                        var adsForRecommend = await result.ToListAsync();
                        adRecomendList = new List<Ad>();

                        adsForRecommend.ForEach(a =>
                        {
                            INode aa = a["ad"].As<INode>();

                            Ad ad = new Ad
                            {
                                Id = (int)aa.Id,
                                Name = aa.Properties["name"].ToString(),
                                Category = aa.Properties["category"].ToString(),
                                Price = aa.Properties["price"].ToString(),
                                Description = aa.Properties["description"].ToString()
                            };

                            adRecomendList.Add(ad);
                        });
                    }
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(new Ads { CategoryList = categoryList, AdList = adList, AdRecomendList = adRecomendList });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Register()
        {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index");

            return View();
        }

        public IActionResult Login()
        {
            if (!HttpContext.Session.IsUsernameEmpty())
                return RedirectToAction("Index");

            return View();
        }

        public async Task<IActionResult> SavedAds()
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            List<Ad> adList = new List<Ad>();
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (u:User)-[r:SAVED]-(ad:Ad) WHERE id(u) = {userId} RETURN ad");
                var ads = await result.ToListAsync();
                ads.ForEach(a =>
                {
                    INode ad1 = a["ad"].As<INode>();

                    Ad ad = new Ad
                    {
                        Id = (int)ad1.Id,
                        Name = ad1.Properties["name"].ToString(),
                        Category = ad1.Properties["category"].ToString(),
                        Price = ad1.Properties["price"].ToString(),
                        Description = ad1.Properties["description"].ToString()
                    };

                    adList.Add(ad);
                });
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(adList);
        }

        public async Task<IActionResult> Profile()
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            User u;
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (u:User) WHERE id(u) = {userId} RETURN u");
                var user1 = await result.SingleAsync();

                INode user = user1["u"].As<INode>();
                u = new User
                {
                    Username = user.Properties["username"].ToString(),
                    Password = user.Properties["password"].ToString(),
                    City = user.Properties["city"].ToString(),
                    Email = user.Properties["email"].ToString(),
                    Phone = user.Properties["phone"].ToString()
                };
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(u);
        }

        public async Task<IActionResult> MineAds()
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            List<Ad> adList = new List<Ad>();
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (u:User)-[r:POSTED]-(ad:Ad) WHERE id(u) = {userId} RETURN ad");
                var ads = await result.ToListAsync();
                ads.ForEach(a =>
                {
                    INode ad1 = a["ad"].As<INode>();

                    Ad ad = new Ad
                    {
                        Id = (int)ad1.Id,
                        Name = ad1.Properties["name"].ToString(),
                        Category = ad1.Properties["category"].ToString(),
                        Price = ad1.Properties["price"].ToString(),
                        Description = ad1.Properties["description"].ToString()
                    };

                    adList.Add(ad);
                });
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(adList);
        }

        public async Task<IActionResult> AdView(int adId)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            Ad ad;
            var statementText = new StringBuilder();
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync($"MATCH (ad:Ad) WHERE id(ad) = {adId} RETURN ad");
                var ad1 = await result.SingleAsync();

                INode ad2 = ad1["ad"].As<INode>();

                ad = new Ad
                {
                    Id = (int)ad2.Id,
                    Name = ad2.Properties["name"].ToString(),
                    Category = ad2.Properties["category"].ToString(),
                    Price = ad2.Properties["price"].ToString(),
                    Description = ad2.Properties["description"].ToString()
                };

                session = _driver.AsyncSession();
                statementText.Append(@$"MATCH (u:User)-[r:VISITED]-(ad:Ad) 
                                    WHERE id(u)={userId} 
                                    RETURN ad, r, u");

                result = await session.RunAsync(statementText.ToString());
                var visitedAds = await result.ToListAsync();

                var rel = visitedAds.Select(rec => rec["r"].As<IRelationship>()).ToList().Find(x => x.EndNodeId == adId);
                long count = 1;
                if (rel != null && rel.Properties.TryGetValue("visitedCount", out object val))
                {
                    count = (long)val + 1;
                }


                if (visitedAds.Count < 6)
                {
                    session = _driver.AsyncSession();
                    statementText.Clear();
                    if (rel == null)
                    {
                        statementText.Append(@$"MATCH(u:User) WHERE id(u)={userId} 
                                                MATCH (ad:Ad) WHERE id(ad)={adId} 
                                                CREATE (u)-[:VISITED {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}]->(ad)");
                    }
                    else
                    {
                        statementText.Append(@$"MATCH(u:User)-[r:VISITED]-(ad:Ad) 
                                                WHERE id(u)={userId} AND id(ad)={adId} 
                                                SET r = {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}");
                    }

                    result = await session.RunAsync(statementText.ToString());
                }
                else
                {
                    long min = visitedAds.Min(x => (long)x["r"].As<IRelationship>().Properties["timestamp"]);
                    session = _driver.AsyncSession();
                    statementText.Clear();
                    statementText.Append(@$"MATCH(u:User)-[r:VISITED]->(ad:Ad) 
                                            WHERE id(u)={userId} AND r.timestamp={min} 
                                            DELETE r");
                    result = await session.RunAsync(statementText.ToString());


                    session = _driver.AsyncSession();
                    statementText.Clear();
                    if (rel == null)
                    {
                        statementText.Append(@$"MATCH(u:User) WHERE id(u)={userId} 
                                                MATCH (ad:Ad) WHERE id(ad)={adId} 
                                                CREATE (u)-[:VISITED {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}]->(ad)");
                    }
                    else
                    {
                        statementText.Append(@$"MATCH(u:User)-[r:VISITED]-(ad:Ad) 
                                                WHERE id(u)={userId} AND id(ad)={adId} 
                                                SET r = {{ timestamp: {DateTime.Now.Ticks}, visitedCount: {count} }}");
                    }
                    result = await session.RunAsync(statementText.ToString());
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(ad);
        }

        public async Task<IActionResult> ChangeAd(string id, string name, string category, string price, string descritpion)
        {
            int adId = int.Parse(id);
            var statementText = new StringBuilder();
            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                statementText.Append(@$"MATCH (ad:Ad) 
                                    WHERE id(ad)={adId}
                                    SET ad = {{ name: '{name}', category: '{category}', price: '{price}', description: '{descritpion}' }} ");

                result = await session.RunAsync(statementText.ToString());
            }
            finally
            {
                await session.CloseAsync();
            }

            return RedirectToAction("MineAds", "Home");
        }
    }
}
