using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjekatNBP.Extensions;
using ProjekatNBP.Session;
using ProjekatNBP.Models;
using System.Linq;

namespace ProjekatNBP.Controllers
{
    public class ChatController : Controller
    {
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

        public IActionResult StartConversation(string room)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            string[] pom = room.Split(':');

            RedisManager<Room>.SetPush($"users:{userId}:rooms", new Room(room));
            RedisManager<Room>.SetPush($"users:{pom[2]}:rooms", new Room(room));

            return RedirectToAction("Index", "Chat", new { room=room });
        }
    }
}
