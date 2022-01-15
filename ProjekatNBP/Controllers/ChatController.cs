using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjekatNBP.Extensions;
using ProjekatNBP.Session;
using ProjekatNBP.Models;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Neo4j.Driver;
using System.Threading.Tasks;
using System;

namespace ProjekatNBP.Controllers
{
    public class ChatController : Controller
    {
        private readonly IDriver _driver;

        public ChatController(IDriver dirver)
        {
            _driver = dirver;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetUserId();
            if (!HttpContext.Session.IsLoggedIn())
                return RedirectToAction("Login", "Home");

            return View(RedisManager<Room>.GetAllFromSet($"users:{userId}:rooms").ToArray());
        }

        public IActionResult GetMessages(string room)
        {
            var userId = HttpContext.Session.GetUserId();
            var msgs = RedisManager<Message>.GetAll($"rooms:{room}");

            for (int i = 0; i < msgs.Length; i++)
                if(!msgs[i].Read && msgs[i].From != userId)
                    RedisManager<Message>.Update($"rooms:{room}", i, msgs[i] with { Read = true });

            return Json(msgs);
        }

        public async Task<IActionResult> StartConversation(string adName,string room)
        {
            var userId = HttpContext.Session.GetUserId();
            var pom = room.Split(':');

            Dictionary<int, string> participants = new();

            IAsyncSession session = _driver.AsyncSession();
            try
            {
                var result = await session.RunAsync($"MATCH (u:User) WHERE id(u) = {userId} OR id(u) = {pom[2]} RETURN u");
                var l = await result.ToListAsync();
                participants = l.ToDictionary(x => (int)(x["u"].As<INode>()).Id, x => x["u"].As<INode>().Properties["username"].ToString());
            }
            finally { await session.CloseAsync(); }

            var r = new Room(room, adName, participants);

            RedisManager<Room>.SetPush($"users:{userId}:rooms", r);
            RedisManager<Room>.SetPush($"users:{pom[2]}:rooms", r);

            return RedirectToAction("Index", "Chat", new { room, adName });
        }

        [HttpPost]
        public async Task<IActionResult> LikeUser(int userId)
        {
            IAsyncSession session = _driver.AsyncSession();
            try {
                var result = await session.RunAsync($"MATCH (u:User) WHERE id(u) = {userId} RETURN u.username");
                var item = await result.SingleAsync();
                var userName = item.Values[item.Keys[0]].ToString();
                RedisManager<UserInfo>.IncrementSortedSet($"leaderboard", new(userId, userName));
                return Json(new { success = true });
            }
            catch(Exception e) {
                return Json(new { success = false, error = e.Message });
            }
            finally {
                await session.CloseAsync();
            }
        }
    }
}
