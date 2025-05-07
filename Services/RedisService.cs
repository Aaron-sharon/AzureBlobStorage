using StackExchange.Redis;

public class RedisService
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisService()
    {
        _redis = ConnectionMultiplexer.Connect("localhost:6379");
        _db = _redis.GetDatabase();
    }

    /// <summary>
    /// Store a simple key-value pair in Redis.
    /// </summary>
    public async Task SetDataAsync(string key, string value)
    {
        await _db.StringSetAsync(key, value);
    }

    /// <summary>
    /// Retrieve a value by key from Redis.
    /// </summary>
    public async Task<string?> GetDataAsync(string key)
    {
        return await _db.StringGetAsync(key);
    }

    /// <summary>
    /// Enqueue a message to a Redis list.
    /// </summary>
    public async Task<long> EnqueueAsync(string queueName, string message)
    {
        return await _db.ListRightPushAsync(queueName, message);
    }

    /// <summary>
    /// Dequeue a message from a Redis list.
    /// </summary>
    public async Task<string?> DequeueAsync(string queueName)
    {
        return await _db.ListLeftPopAsync(queueName);
    }

    /// <summary>
    /// Get the length of a Redis list.
    /// </summary>
    public async Task<long> GetQueueLengthAsync(string queueName)
    {
        return await _db.ListLengthAsync(queueName);
    }
}
