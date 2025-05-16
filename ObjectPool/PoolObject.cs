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
    
    public static int MaxPoolSize
    {
        get => GetPoolData().MaxSize;
        set
        {
            var data = GetPoolData();
            lock (data.Lock)
            {
                data.MaxSize = value;
                if (data.CurrentPoolSize > value)
                {
                    Clear();
                }
            }
        }
    }
    public static int CountActive => GetPoolData().CurrentActiveCount;
    public static int CountInactive => GetPoolData().Pool.Count;
    public static int CurrentSize =>  GetPoolData().CurrentPoolSize;
    
    public static T Get()
    {
        var data = GetPoolData();
        if (data.Pool.TryTake(out var item))
        {
            Interlocked.Increment(ref data.CurrentActiveCount);
            return item;
        }
        lock (data.Lock)
        {
            if (data.CurrentPoolSize < data.MaxSize)
            {
                var obj = Activator.CreateInstance<T>();
                Interlocked.Increment(ref data.CurrentPoolSize);
                Interlocked.Increment(ref data.CurrentActiveCount);
                return obj!;
            }

            throw new InvalidOperationException($"Pool hat die maximale Größe von {data.MaxSize} erreicht.");
        }
    }

    public static IEnumerable<T> Get(int count)
    {
        var data = GetPoolData();
        var result = new List<T>();
        for (var i = 0; i < count; i++)
        {
            if (data.Pool.TryTake(out var item))
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
    
    public static void Create(int count = 1)
    {
        if (count <= 0) return;
        var data = GetPoolData();
        lock (data.Lock)
        {
            var canCreate = Math.Min(count, data.MaxSize - data.CurrentPoolSize);
            for (var i = 0; i < canCreate; i++)
            {
                var obj = Activator.CreateInstance<T>();
                data.Pool.Add(obj);
                Interlocked.Increment(ref data.CurrentPoolSize);
            }
        }
    }

    public static void Release(T? obj)
    {
        if (obj == null) return;
        var data = GetPoolData();
        if (obj is PoolObject<T> poolObj)
            poolObj.Reset();
        data.Pool.Add(obj);
        Interlocked.Decrement(ref data.CurrentActiveCount);
    }

    public static void Release(IEnumerable<T> objects)
    {
        foreach (var obj in objects)
        {
            Release(obj);
        }
    }
    
    public static void Dispose()
    {
        var data = GetPoolData();
        lock (data.Lock)
        {
            foreach (var obj in data.Pool)
            {
                if (obj is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            data.Pool.Clear();
            data.CurrentPoolSize = 0;
            data.CurrentActiveCount = 0;
        }
    }

    private static void Clear()
    {
        var data = GetPoolData();
        while (data.Pool.TryTake(out _)) { }
        data.CurrentPoolSize = 0;
        data.CurrentActiveCount = 0;
    }

    protected abstract void Reset();
}