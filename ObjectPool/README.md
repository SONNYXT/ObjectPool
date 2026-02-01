# ObjectPool

A high-performance, thread-safe object pooling library for .NET that reduces garbage collection pressure by reusing objects.

## Features

- **High Performance** - Lock-free operations using `ConcurrentQueue` and `Interlocked` operations
- **Thread-Safe** - Fully thread-safe for concurrent access
- **Simple API** - Just inherit from `PoolObject<T>` and implement `Reset()`
- **Automatic Reset** - Objects are automatically reset when returned to the pool
- **Pool Monitoring** - Track active, inactive, and total object counts
- **Configurable** - Set maximum pool size and pre-warm the pool

## Installation

Add a reference to the `ObjectPool` project or copy the `PoolObject.cs` file to your project.

## Quick Start

### 1. Create a Pooled Class

```csharp
public class MyObject : PoolObject<MyObject>
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }

    protected override void Reset()
    {
        // Reset properties to default values when returned to pool
        Name = string.Empty;
        Value = 0;
    }
}
```

### 2. Configure the Pool (Optional)

```csharp
// Configure at application startup
MyObject.Configure(maxSize: 500, preWarmCount: 50);
```

### 3. Use the Pool

```csharp
// Get an object from the pool
var obj = MyObject.Get();
obj.Name = "Example";
obj.Value = 42;

// Use the object...

// Return to pool when done
MyObject.Release(obj);
```

## API Reference

### Configuration

| Method | Description |
|--------|-------------|
| `Configure(int maxSize, int preWarmCount)` | Configure pool size and optionally pre-warm with objects |
| `MaxPoolSize` | Get or set the maximum pool size (default: 1000) |

### Getting Objects

| Method | Description |
|--------|-------------|
| `Get()` | Get an object from the pool (throws if pool is exhausted) |
| `TryGet(out T? item)` | Try to get an object without throwing exceptions |
| `Get(Func<T> factory)` | Get an object using a custom factory for creation |
| `Get(int count)` | Get multiple objects from the pool |

### Returning Objects

| Method | Description |
|--------|-------------|
| `Release(T obj)` | Return an object to the pool |
| `Release(IEnumerable<T> objects)` | Return multiple objects to the pool |
| `Destroy(T obj)` | Remove an object from pool management and dispose it |

### Pool Management

| Method | Description |
|--------|-------------|
| `Create(int count)` | Pre-create objects in the pool |
| `Create(Func<T> factory, int count)` | Pre-create objects using a custom factory |
| `Dispose()` | Dispose all objects and clear the pool |

### Monitoring

| Property | Description |
|----------|-------------|
| `CountActive` | Number of objects currently in use |
| `CountInactive` | Number of objects available in the pool |
| `CurrentSize` | Total number of managed objects |

## Advanced Usage

### Using Scoped Objects (Auto-Release)

Use the `PooledObject<T>` wrapper for automatic release with `using`:

```csharp
using var pooled = PoolObjectExtensions.GetScoped<MyObject>();
pooled.Value.Name = "Scoped Example";
// Object is automatically returned to pool at end of scope
```

### TryGet Pattern

For scenarios where pool exhaustion is expected:

```csharp
if (MyObject.TryGet(out var obj))
{
    try
    {
        // Use object
    }
    finally
    {
        MyObject.Release(obj);
    }
}
else
{
    // Handle pool exhaustion without exceptions
}
```

### Custom Factory

Create objects with specific initialization:

```csharp
var obj = MyObject.Get(() => new MyObject 
{ 
    Name = "Custom",
    Value = 100 
});
```

### Pre-Warming

Pre-create objects to avoid allocation during runtime:

```csharp
// At startup
MyObject.Configure(maxSize: 1000, preWarmCount: 100);

// Or manually
MyObject.Create(100);
```

## Best Practices

1. **Configure Early** - Call `Configure()` at application startup before any `Get()` calls
2. **Always Release** - Ensure objects are returned to the pool, use `try/finally` or scoped objects
3. **Reset Completely** - Implement `Reset()` to clear all state, preventing data leaks between uses
4. **Don't Hold References** - After `Release()`, don't keep references to the object
5. **Pre-Warm for Performance** - Use `preWarmCount` to avoid allocations during peak load

## Performance

This implementation uses several optimizations:

- **ConcurrentQueue** instead of ConcurrentBag for better FIFO semantics and cache locality
- **SpinLock** instead of Monitor for short critical sections
- **Lock-free CAS operations** for pool size management
- **AggressiveInlining** on hot paths
- **Fast/Slow path separation** for better JIT optimization

## Thread Safety

All public methods are thread-safe and can be called concurrently from multiple threads.

## License

MIT License - Feel free to use in your projects.
