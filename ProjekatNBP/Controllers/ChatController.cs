using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjekatNBP.Extensions;
using ProjekatNBP.Models;
using ProjekatNBP.Session;

namespace ProjekatNBP.Controllers
{
	public class ChatController : Controller
    {
        private readonly List<Room> rooms = new();

        public IActionResult Index()
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            if (HttpContext.Session.IsUsernameEmpty() || userId == -1)
                return RedirectToAction("Login", "Home");

            return View(rooms);
        }

        public IActionResult GetMessages(string room)
        {
            int userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? -1;
            var msgs = RedisManager<Message>.GetAll($"users:{userId}:room:{room}");

            for (int i = 0; i < msgs.Length; i++)
                if(!msgs[i].Read)
                    RedisManager<Message>.Update($"users:{userId}:room:{room}", i, msgs[i] with { Read = true });

            return Json(msgs);
        }
    }
}
