using System;
using System.Collections.Generic;

namespace ProjekatNBP.Models
{
    public record Room(string Key, string AdName = "", Dictionary<int, string> Participants = null)
    {
        public static Message[] GetMessages(string id) => RedisManager<Message>.GetAll($"rooms:{id}");
        public static void SendMessage(string id, Message msg) => RedisManager<Message>.Push($"rooms:{id}", msg);
    }
}
