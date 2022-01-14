using System;

namespace ProjekatNBP.Models
{
    public record Room(string Key)
    {
        public Message[] GetMessages() => RedisManager<Message>.GetAll($"rooms:{Key}");
        public void SendMessage(Message msg) => RedisManager<Message>.Push($"rooms:{Key}", msg);
    }
}
