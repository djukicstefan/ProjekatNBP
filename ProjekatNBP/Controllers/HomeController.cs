using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using ProjekatNBP.Extensions;
using ProjekatNBP.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    }
}
