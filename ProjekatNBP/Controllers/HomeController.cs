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

        public async Task<IActionResult> Index()
        {
            List<Category> categoryList = new List<Category>();
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

                for(int i = 0; i < categoryList.Count; i++)
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
            }
            finally
            {
                await session.CloseAsync();
            }

            return View(categoryList);
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
    }
}
