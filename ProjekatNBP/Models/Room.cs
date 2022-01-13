using System;

namespace ProjekatNBP.Models
{
    public record Room(string Key)
    {
        public event Action<Message> MessageReceived;

        public Message[] GetMessages() => RedisManager<Message>.GetAll(Key);
        public void SendMessage(Message msg) => RedisManager<Message>.Push(msg.From, msg);
    }
}
