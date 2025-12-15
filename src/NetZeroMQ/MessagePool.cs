using Microsoft.Extensions.ObjectPool;

namespace NetZeroMQ;

/// <summary>
/// Object pool for Message instances to reduce allocations.
/// </summary>
public static class MessagePool
{
    private static readonly ObjectPool<Message> Pool = new DefaultObjectPool<Message>(
        new MessagePoolPolicy(),
        Environment.ProcessorCount * 4);

    /// <summary>
    /// Rents a message from the pool.
    /// </summary>
    /// <returns>A message instance from the pool.</returns>
    public static Message Rent()
    {
        return Pool.Get();
    }

    /// <summary>
    /// Returns a message to the pool.
    /// </summary>
    /// <param name="message">The message to return to the pool.</param>
    public static void Return(Message message)
    {
        if (message != null)
        {
            Pool.Return(message);
        }
    }

    private sealed class MessagePoolPolicy : IPooledObjectPolicy<Message>
    {
        public Message Create()
        {
            return new Message();
        }

        public bool Return(Message obj)
        {
            // Rebuild the message to reset its state
            try
            {
                obj.Rebuild();
                return true;
            }
            catch
            {
                // If rebuild fails, don't return to pool
                obj.Dispose();
                return false;
            }
        }
    }
}
