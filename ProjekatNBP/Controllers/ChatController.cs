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
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            return View(RedisManager<Room>.GetAllFromSet($"users:{userId}:rooms").ToArray());
        }

        public IActionResult GetMessages(string room)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            var msgs = RedisManager<Message>.GetAll($"rooms:{room}");

            for (int i = 0; i < msgs.Length; i++)
                if(!msgs[i].Read && msgs[i].From != userId)
                    RedisManager<Message>.Update($"rooms:{room}", i, msgs[i] with { Read = true });

            return Json(msgs);
        }

        public async Task<IActionResult> StartConversation(string adName,string room)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            string[] pom = room.Split(':');

            Dictionary<int, string> participants = new();

            var statementText = new StringBuilder();
            statementText.Append($"MATCH (u:User) WHERE id(u) = {userId} OR id(u) = {pom[2]} RETURN u");

            IResultCursor result;
            IAsyncSession session = _driver.AsyncSession();
            try
            {
                result = await session.RunAsync(statementText.ToString());
                var users = await result.ToListAsync();
                users.ForEach(x => {
                    INode node = x["u"].As<INode>();
                    participants[(int)node.Id] = node.Properties["username"].ToString();
                });
            }
            finally { await session.CloseAsync(); }

            Room r = new(room, adName, participants);

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
