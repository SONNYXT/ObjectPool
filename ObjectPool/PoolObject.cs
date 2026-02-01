using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ObjectPool;

/// <summary>
/// Abstract base class that implements a high-performance object pool pattern for any reference type.
/// Objects are managed in a thread-safe pool to reduce garbage collection pressure.
/// </summary>
/// <typeparam name="T">The type of object to pool. Must be a class with a parameterless constructor.</typeparam>
public abstract class PoolObject<T> where T : class, new()
{
    private static readonly ConcurrentDictionary<Type, PoolData> Pools = new();

    private sealed class PoolData
    {
        // ConcurrentQueue provides better FIFO semantics and less cache contention than ConcurrentBag
        public readonly ConcurrentQueue<T> Pool = new();
        public int CurrentPoolSize;
        public int CurrentActiveCount;
        public int MaxSize = 1000;

        // SpinLock is faster than Monitor for short critical sections
        private SpinLock _spinLock = new(enableThreadOwnerTracking: false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterLock(ref bool lockTaken) => _spinLock.Enter(ref lockTaken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitLock() => _spinLock.Exit(useMemoryBarrier: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PoolData GetPoolData()
    {
        return Pools.GetOrAdd(typeof(T), static _ => new PoolData());
    }

    /// <summary>
    /// Configures the pool with the specified options. Should be called before first use.
    /// </summary>
    /// <param name="maxSize">The maximum number of objects the pool can hold. Default is 1000.</param>
    /// <param name="preWarmCount">Optional number of objects to pre-create in the pool.</param>
    /// <remarks>
    /// This method should ideally be called once at application startup before any Get() calls.
    /// If the pool is already in use, existing objects will be preserved unless the new maxSize
    /// is smaller than the current pool size (in which case the pool will be cleared).
    /// </remarks>
    /// <example>
    /// // Configure at startup
    /// TestModel.Configure(maxSize: 500, preWarmCount: 50);
    /// </example>
    public static void Configure(int maxSize = 1000, int preWarmCount = 0)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be greater than 0.");

        var data = GetPoolData();
        var lockTaken = false;
        try
        {
            data.EnterLock(ref lockTaken);
            data.MaxSize = maxSize;

            // If the pool is too large, clear it
            if (Volatile.Read(ref data.CurrentPoolSize) > maxSize)
            {
                ClearInternal(data);
            }
        }
        finally
        {
            if (lockTaken) data.ExitLock();
        }

        // Pre-warm the pool if requested
        if (preWarmCount > 0)
        {
            Create(preWarmCount);
        }
    }

    /// <summary>
    /// Gets or sets the maximum size of the pool.
    /// If the current pool size exceeds the new value, the pool will be cleared.
    /// </summary>
    /// <remarks>
    /// Setting a smaller value than the current pool size will clear the pool completely.
    /// Default value is 1000.
    /// </remarks>
    public static int MaxPoolSize
    {
        get => Volatile.Read(ref GetPoolData().MaxSize);
        set
        {
            var data = GetPoolData();
            var lockTaken = false;
            try
            {
                data.EnterLock(ref lockTaken);
                data.MaxSize = value;
                // If the pool is too large, clear it to match the new maximum size
                if (Volatile.Read(ref data.CurrentPoolSize) > value)
                {
                    ClearInternal(data);
                }
            }
            finally
            {
                if (lockTaken) data.ExitLock();
            }
        }
    }

    /// <summary>
    /// Returns the number of currently active (checked out) objects in the pool.
    /// </summary>
    /// <remarks>
    /// Active objects are those that have been retrieved with Get() but not yet returned with Release().
    /// </remarks>
    public static int CountActive
    {
        get => Volatile.Read(ref GetPoolData().CurrentActiveCount);
    }

    /// <summary>
    /// Returns the number of currently inactive (available) objects in the pool.
    /// </summary>
    /// <remarks>
    /// Inactive objects are those currently available in the pool, ready to be retrieved.
    /// </remarks>
    public static int CountInactive
    {
        get => GetPoolData().Pool.Count;
    }

    /// <summary>
    /// Returns the current size of the pool (total managed objects).
    /// </summary>
    /// <remarks>
    /// This includes both active and inactive objects being managed by the pool.
    /// </remarks>
    public static int CurrentSize
    {
        get => Volatile.Read(ref GetPoolData().CurrentPoolSize);
    }

    /// <summary>
    /// Retrieves an object from the pool or creates a new one if the pool is empty and not full.
    /// </summary>
    /// <returns>An instance of T from the pool.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the pool is at maximum capacity and no more objects can be created.
    /// </exception>
    /// <remarks>
    /// This method is thread-safe and can be called concurrently.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get()
    {
        var data = GetPoolData();

        // Fast path: try to get an object from the pool without locking
        if (!data.Pool.TryDequeue(out var item)) return GetSlow(data);
        Interlocked.Increment(ref data.CurrentActiveCount);
        return item;

        // Slow path: need to create a new object
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T GetSlow(PoolData data)
    {
        // Try lock-free creation first using Compare-And-Swap
        var currentSize = Volatile.Read(ref data.CurrentPoolSize);
        var maxSize = Volatile.Read(ref data.MaxSize);

        while (currentSize < maxSize)
        {
            if (Interlocked.CompareExchange(ref data.CurrentPoolSize, currentSize + 1, currentSize) == currentSize)
            {
                // Successfully reserved a slot, create the object
                var obj = new T();
                Interlocked.Increment(ref data.CurrentActiveCount);
                return obj;
            }
            // CAS failed, re-read and retry
            currentSize = Volatile.Read(ref data.CurrentPoolSize);
            maxSize = Volatile.Read(ref data.MaxSize);
        }

        // Pool is at maximum size
        throw new InvalidOperationException($"Pool has reached maximum size of {maxSize}.");
    }

    /// <summary>
    /// Attempts to retrieve an object from the pool without throwing an exception.
    /// </summary>
    /// <param name="item">When this method returns, contains the retrieved object if successful; otherwise, null.</param>
    /// <returns>true if an object was successfully retrieved; otherwise, false.</returns>
    /// <remarks>
    /// This method is preferred over Get() when pool exhaustion is expected.
    /// It avoids the overhead of exception handling.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet(out T? item)
    {
        var data = GetPoolData();

        // Fast path: try to get an object from the pool
        if (!data.Pool.TryDequeue(out item)) return TryGetSlow(data, out item);
        Interlocked.Increment(ref data.CurrentActiveCount);
        return true;

        // Slow path: try to create a new object
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetSlow(PoolData data, out T? item)
    {
        var currentSize = Volatile.Read(ref data.CurrentPoolSize);
        var maxSize = Volatile.Read(ref data.MaxSize);

        while (currentSize < maxSize)
        {
            if (Interlocked.CompareExchange(ref data.CurrentPoolSize, currentSize + 1, currentSize) == currentSize)
            {
                item = new T();
                Interlocked.Increment(ref data.CurrentActiveCount);
                return true;
            }
            currentSize = Volatile.Read(ref data.CurrentPoolSize);
            maxSize = Volatile.Read(ref data.MaxSize);
        }

        item = null;
        return false;
    }

    /// <summary>
    /// Retrieves an object from the pool using a custom factory function, or creates a new one if the pool is empty and not full.
    /// </summary>
    /// <param name="factory">A function that creates a new instance of T if needed.</param>
    /// <returns>An instance of T from the pool, or a new one created by the factory.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the pool is at maximum capacity and no more objects can be created.
    /// </exception>
    /// <remarks>
    /// This method is thread-safe and can be called concurrently.
    /// The factory function is only called if a new object needs to be created.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get(Func<T> factory)
    {
        var data = GetPoolData();

        // Fast path: try to get an object from the pool
        if (!data.Pool.TryDequeue(out var item)) return GetSlowWithFactory(data, factory);
        Interlocked.Increment(ref data.CurrentActiveCount);
        return item;

        // Slow path: create using factory
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T GetSlowWithFactory(PoolData data, Func<T> factory)
    {
        var currentSize = Volatile.Read(ref data.CurrentPoolSize);
        var maxSize = Volatile.Read(ref data.MaxSize);

        while (currentSize < maxSize)
        {
            if (Interlocked.CompareExchange(ref data.CurrentPoolSize, currentSize + 1, currentSize) == currentSize)
            {
                var obj = factory();
                Interlocked.Increment(ref data.CurrentActiveCount);
                return obj;
            }
            currentSize = Volatile.Read(ref data.CurrentPoolSize);
            maxSize = Volatile.Read(ref data.MaxSize);
        }

        throw new InvalidOperationException($"Pool has reached maximum size of {maxSize}.");
    }

    /// <summary>
    /// Retrieves multiple objects from the pool.
    /// </summary>
    /// <param name="count">The number of objects to retrieve.</param>
    /// <returns>A collection of objects from the pool. May contain fewer items than requested if the pool is running low.</returns>
    /// <remarks>
    /// This method will not create new objects if the pool is empty.
    /// It will return as many objects as are currently available in the pool, up to the requested count.
    /// </remarks>
    public static IEnumerable<T> Get(int count)
    {
        var data = GetPoolData();
        var result = new List<T>(Math.Min(count, data.Pool.Count));

        for (var i = 0; i < count; i++)
        {
            if (data.Pool.TryDequeue(out var item))
            {
                result.Add(item);
                Interlocked.Increment(ref data.CurrentActiveCount);
            }
            else
            {
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
    /// If the pool is already at maximum capacity, no new objects will be created.
    /// </remarks>
    public static void Create(int count = 1)
    {
        if (count <= 0) return;
        var data = GetPoolData();

        for (var i = 0; i < count; i++)
        {
            var currentSize = Volatile.Read(ref data.CurrentPoolSize);
            var maxSize = Volatile.Read(ref data.MaxSize);

            if (currentSize >= maxSize) break;

            if (Interlocked.CompareExchange(ref data.CurrentPoolSize, currentSize + 1, currentSize) == currentSize)
            {
                var obj = new T();
                data.Pool.Enqueue(obj);
            }
            else
            {
                // CAS failed, retry this iteration
                i--;
            }
        }
    }

    /// <summary>
    /// Pre-fills the pool with a specified number of objects created by the provided factory.
    /// </summary>
    /// <param name="factory">A function that creates new instances of T.</param>
    /// <param name="count">The number of objects to create. Defaults to 1.</param>
    /// <remarks>
    /// This method allows customization of the objects added to the pool.
    /// It will only create objects up to the maximum pool size.
    /// If the pool is already at maximum capacity, no new objects will be created.
    /// </remarks>
    public static void Create(Func<T> factory, int count = 1)
    {
        if (count <= 0) return;
        var data = GetPoolData();

        for (var i = 0; i < count; i++)
        {
            var currentSize = Volatile.Read(ref data.CurrentPoolSize);
            var maxSize = Volatile.Read(ref data.MaxSize);

            if (currentSize >= maxSize) break;

            if (Interlocked.CompareExchange(ref data.CurrentPoolSize, currentSize + 1, currentSize) == currentSize)
            {
                var obj = factory();
                data.Pool.Enqueue(obj);
            }
            else
            {
                i--;
            }
        }
    }

    /// <summary>
    /// Releases an object back to the pool and resets its state if applicable.
    /// </summary>
    /// <param name="obj">The object to release back to the pool.</param>
    /// <remarks>
    /// If the object is a subclass of PoolObject&lt;T&gt; its Reset method will be called.
    /// Passing null is safe and will be ignored.
    /// This method is thread-safe and can be called concurrently.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Release(T? obj)
    {
        if (obj == null) return;
        var data = GetPoolData();

        // Reset the object state if it's a PoolObject
        if (obj is PoolObject<T> poolObj)
            poolObj.Reset();

        // Return the object to the pool
        data.Pool.Enqueue(obj);
        Interlocked.Decrement(ref data.CurrentActiveCount);
    }

    /// <summary>
    /// Releases multiple objects back to the pool.
    /// </summary>
    /// <param name="objects">The collection of objects to release.</param>
    /// <remarks>
    /// This is a convenience method that calls Release for each object in the collection.
    /// Null objects in the collection will be ignored.
    /// </remarks>
    public static void Release(IEnumerable<T> objects)
    {
        foreach (var obj in objects)
        {
            Release(obj);
        }
    }

    /// <summary>
    /// Removes an active object from pool management and disposes it if applicable.
    /// </summary>
    /// <param name="obj">The active object to destroy.</param>
    /// <remarks>
    /// This method permanently removes an active object from pool tracking and reduces the pool size.
    /// Use this for objects that should not be returned to the pool (e.g., corrupted state).
    /// If the object implements IDisposable, it will be properly disposed.
    /// This method is thread-safe.
    /// </remarks>
    public static void Destroy(T? obj)
    {
        if (obj == null) return;
        var data = GetPoolData();

        // Decrement counters for the destroyed object
        Interlocked.Decrement(ref data.CurrentPoolSize);
        Interlocked.Decrement(ref data.CurrentActiveCount);

        // Dispose the object if it implements IDisposable
        if (obj is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Disposes all IDisposable objects in the pool and clears it.
    /// </summary>
    /// <remarks>
    /// Call this method when you no longer need the pool to release unmanaged resources.
    /// This method will properly dispose any objects that implement IDisposable.
    /// After calling this method, the pool will be empty but still usable.
    /// This method is thread-safe.
    /// </remarks>
    public static void Dispose()
    {
        var data = GetPoolData();
        var lockTaken = false;
        try
        {
            data.EnterLock(ref lockTaken);

            // Dispose any IDisposable objects in the pool
            while (data.Pool.TryDequeue(out var obj))
            {
                if (obj is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            // Reset counters
            data.CurrentPoolSize = 0;
            data.CurrentActiveCount = 0;
        }
        finally
        {
            if (lockTaken) data.ExitLock();
        }
    }

    /// <summary>
    /// Clears the pool and resets counters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClearInternal(PoolData data)
    {
        while (data.Pool.TryDequeue(out _)) { }
        data.CurrentPoolSize = 0;
        data.CurrentActiveCount = 0;
    }

    /// <summary>
    /// Resets the state of the pooled object. Must be implemented in derived classes.
    /// </summary>
    /// <remarks>
    /// This method is called automatically when an object is returned to the pool.
    /// Override this method to reset properties to their default values.
    /// This ensures objects are in a clean state when retrieved from the pool later.
    /// </remarks>
    protected abstract void Reset();
}

/// <summary>
/// A disposable wrapper that automatically returns the pooled object when disposed.
/// Enables using-statement pattern for automatic resource management.
/// </summary>
/// <typeparam name="T">The type of pooled object.</typeparam>
public readonly struct PooledObject<T> : IDisposable where T : PoolObject<T>, new()
{
    /// <summary>
    /// Gets the pooled object instance.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets whether this instance contains a valid object.
    /// </summary>
    public bool HasValue
    {
        get => Value != null;
    }

    internal PooledObject(T value)
    {
        Value = value;
    }

    /// <summary>
    /// Returns the object to the pool.
    /// </summary>
    public void Dispose()
    {
        if (Value != null)
        {
            PoolObject<T>.Release(Value);
        }
    }

    /// <summary>
    /// Implicit conversion to the underlying type.
    /// </summary>
    public static implicit operator T(PooledObject<T> pooled) => pooled.Value;
}