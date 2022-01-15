using StackExchange.Redis;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace ProjekatNBP
{
    public static class RedisManager
    {
        private static readonly ConnectionMultiplexer redis;
        public static IDatabase Database { get; private set; }
        public static ISubscriber Subscriber { get; private set; }

        static RedisManager()
        {
            redis = ConnectionMultiplexer.Connect(new ConfigurationOptions { EndPoints = { "localhost:6379" } });
            Database = redis.GetDatabase();
            Subscriber = redis.GetSubscriber();
        }
    }

    public static class RedisManager<T>
    {
        private static T Deserialize(RedisValue data) => JsonConvert.DeserializeObject<T>(data);
        private static string Serialize(T data) => JsonConvert.SerializeObject(data);

        public static void Push(string path, T data)
            => RedisManager.Database.ListRightPush(path, Serialize(data));

        public static void SetPush(string path, T data)
            => RedisManager.Database.SetAdd(path, Serialize(data));

        public static T[] GetAll(string path, int start = 0, int stop = -1)
            => RedisManager.Database.ListRange(path, start, stop).Select(Deserialize).ToArray();

        public static T[] GetAllFromSet(string path)
            => RedisManager.Database.SetMembers(path).Select(Deserialize).ToArray();

        public static void Update(string path, int index, T data)
            => RedisManager.Database.ListSetByIndex(path, index, Serialize(data));

        public static T Pop(string path)
            => Deserialize(RedisManager.Database.ListRightPop(path));

        public static T GetByIndex(string path, int index)
            => Deserialize(RedisManager.Database.ListGetByIndex(path, index));

        public static bool DeleteItem(string path, T item) 
            => RedisManager.Database.ListRemove(path, Serialize(item)) > 0;

        public static void Subscribe(string path, Action<string, T> onPub)
            => RedisManager.Subscriber.Subscribe(path, (x, y) => onPub(x, Deserialize(y)));

        public static void Publish(string path, T data)
            => RedisManager.Database.Publish(path, Serialize(data));
    }
}
