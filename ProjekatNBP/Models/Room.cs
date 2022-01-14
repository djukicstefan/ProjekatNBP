using System;
using System.Collections.Generic;

namespace ProjekatNBP.Models
{
    public record Room(string Key, string adName = "", Dictionary<int, string> participants = null)
    {
        public Message[] GetMessages() => RedisManager<Message>.GetAll($"rooms:{Key}");
        public void SendMessage(Message msg) => RedisManager<Message>.Push($"rooms:{Key}", msg);
    }
}
