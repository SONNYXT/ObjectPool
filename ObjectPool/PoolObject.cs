using System.Collections.Concurrent;

namespace ObjectPool;

public abstract class PoolObject<T> where T : class, new()
{
    private static readonly ConcurrentDictionary<Type, PoolData> Pools = new();
    
    private class PoolData
    {
        public readonly ConcurrentBag<T> Pool = [];
        public readonly object Lock = new();
        public int CurrentPoolSize;
        public int CurrentActiveCount;
        public int MaxSize = 1000;
    }

    private static PoolData GetPoolData()
    {
        return Pools.GetOrAdd(typeof(T), _ => new PoolData());
    }
    
    /// <summary>
    /// Gets or sets the maximum size of the pool.
    /// If the current pool size exceeds the new value, the pool will be cleared.
    /// </summary>
    public static int MaxPoolSize
    {
        get => GetPoolData().MaxSize;
        set
        {
            var data = GetPoolData();
            lock (data.Lock)
            {
                data.MaxSize = value;
                // If the pool is too large, clear it to match the new maximum size
                if (data.CurrentPoolSize > value)
                {
                    Clear();
                }
            }
        }
    }

    /// <summary>
    /// Returns the number of currently active (checked out) objects in the pool.
    /// </summary>
    public static int CountActive => GetPoolData().CurrentActiveCount;

    /// <summary>
    /// Returns the number of currently inactive (available) objects in the pool.
    /// </summary>
    public static int CountInactive => GetPoolData().Pool.Count;

    /// <summary>
    /// Returns the current size of the pool (total managed objects).
    /// </summary>
    public static int CurrentSize =>  GetPoolData().CurrentPoolSize;
    
    /// <summary>
    /// Retrieves an object from the pool or creates a new one if the pool is not full.
    /// </summary>
    /// <returns>An instance of T from the pool.</returns>
    public static T Get()
    {
        var data = GetPoolData();
        // Try to get an object from the pool
        if (data.Pool.TryTake(out var item))
        {
            Interlocked.Increment(ref data.CurrentActiveCount);
            return item;
        }
        lock (data.Lock)
        {
            // If the pool is not full, create a new object
            if (data.CurrentPoolSize < data.MaxSize)
            {
                var obj = Activator.CreateInstance<T>();
                Interlocked.Increment(ref data.CurrentPoolSize);
                Interlocked.Increment(ref data.CurrentActiveCount);
                return obj!;
            }
            // Pool is at maximum size, throw exception
            throw new InvalidOperationException($"Pool hat die maximale Größe von {data.MaxSize} erreicht.");
        }
    }
    
    /// <summary>
    /// Retrieves an object from the pool using a custom factory function, or creates a new one if the pool is not full.
    /// </summary>
    /// <param name="factory">A function that creates a new instance of T if needed.</param>
    /// <returns>An instance of T from the pool, or a new one created by the factory.</returns>
    public static T? Get(Func<T> factory)
    {
        var data = GetPoolData();
        // Try to get an object from the pool
        if (data.Pool.TryTake(out var item))
        {
            Interlocked.Increment(ref data.CurrentActiveCount);
            return item;
        }
        lock (data.Lock)
        {
            // If the pool is not full, create a new object using the factory function
            if (data.CurrentPoolSize < data.MaxSize)
            {
                var obj = factory();
                Interlocked.Increment(ref data.CurrentPoolSize);
                Interlocked.Increment(ref data.CurrentActiveCount);
                return obj;
            }
            // Pool is at maximum size, throw exception
            throw new InvalidOperationException($"Pool hat die maximale Größe von {data.MaxSize} erreicht.");
        }
    }

    /// <summary>
    /// Retrieves multiple objects from the pool.
    /// </summary>
    /// <param name="count">The number of objects to retrieve.</param>
    /// <returns>A collection of objects from the pool. May contain fewer items than requested if pool is running low.</returns>
    public static IEnumerable<T> Get(int count)
    {
        var data = GetPoolData();
        var result = new List<T>();
        // Try to get requested number of objects from the pool
        for (var i = 0; i < count; i++)
        {
            if (data.Pool.TryTake(out var item))
            {
                result.Add(item);
                Interlocked.Increment(ref data.CurrentActiveCount);
            }
            else
            {
                // Pool is empty, break the loop
                break;
            }
        }
        return result;
    }
    
    /// <summary>
    /// Pre-fills the pool with a specified number of objects.
    /// </summary>
    /// <param name="count">The number of objects to create. Defaults to 1.</param>
    /// <remarks>
    /// This method is useful for pre-warming the pool, reducing allocation overhead during normal operation.
    /// It will only create objects up to the maximum pool size.
    /// </remarks>
    public static void Create(int count = 1)
    {
        if (count <= 0) return;
        var data = GetPoolData();
        lock (data.Lock)
        {
            // Calculate how many objects we can actually create without exceeding the max size
            var canCreate = Math.Min(count, data.MaxSize - data.CurrentPoolSize);
            for (var i = 0; i < canCreate; i++)
            {
                var obj = Activator.CreateInstance<T>();
                data.Pool.Add(obj);
                Interlocked.Increment(ref data.CurrentPoolSize);
            }
        }
    }

    /// <summary>
    /// Releases an object back to the pool and resets its state if applicable.
    /// </summary>
    /// <param name="obj">The object to release back to the pool.</param>
    public static void Release(T? obj)
    {
        if (obj == null) return;
        var data = GetPoolData();
        // Reset the object state if it's a PoolObject
        if (obj is PoolObject<T> poolObj)
            poolObj.Reset();
        // Return the object to the pool
        data.Pool.Add(obj);
        Interlocked.Decrement(ref data.CurrentActiveCount);
    }

    /// <summary>
    /// Releases multiple objects back to the pool.
    /// </summary>
    /// <param name="objects">The collection of objects to release.</param>
    public static void Release(IEnumerable<T> objects)
    {
        // Release each object individually
        foreach (var obj in objects)
        {
            Release(obj);
        }
    }
    
    /// <summary>
    /// Disposes all IDisposable objects in the pool and clears it.
    /// </summary>
    /// <remarks>
    /// Call this method when you no longer need the pool to release unmanaged resources.
    /// </remarks>
    public static void Dispose()
    {
        var data = GetPoolData();
        lock (data.Lock)
        {
            // Dispose any IDisposable objects in the pool
            foreach (var obj in data.Pool)
            {
                if (obj is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            // Clear the pool and reset counters
            data.Pool.Clear();
            data.CurrentPoolSize = 0;
            data.CurrentActiveCount = 0;
        }
    }

    /// <summary>
    /// Clears the pool and resets counters.
    /// </summary>
    /// <remarks>
    /// Internal use only. To properly dispose objects, use Dispose() instead.
    /// </remarks>
    private static void Clear()
    {
        var data = GetPoolData();
        // Remove all objects from the pool without disposing them
        while (data.Pool.TryTake(out _)) { }
        data.CurrentPoolSize = 0;
        data.CurrentActiveCount = 0;
    }

    /// <summary>
    /// Resets the state of the pooled object. Must be implemented in derived classes.
    /// </summary>
    /// <remarks>
    /// This method is called when an object is returned to the pool.
    /// Override this method to reset properties to default values.
    /// </remarks>
    protected abstract void Reset();
}