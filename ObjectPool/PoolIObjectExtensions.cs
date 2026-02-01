using System.Runtime.CompilerServices;

namespace ObjectPool;

/// <summary>
/// Extension methods for PoolObject to support the PooledObject wrapper.
/// </summary>
public static class PoolObjectExtensions
{
    /// <summary>
    /// Gets an object from the pool wrapped in a PooledObject for automatic disposal.
    /// </summary>
    /// <typeparam name="T">The type of pooled object.</typeparam>
    /// <returns>A PooledObject wrapper that will return the object to the pool when disposed.</returns>
    /// <example>
    /// using var pooled = PoolObjectExtensions.GetScoped&lt;MyClass&gt;();
    /// pooled.Value.DoSomething();
    /// // Object is automatically returned to pool at end of scope
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PooledObject<T> GetScoped<T>() where T : PoolObject<T>, new()
    {
        return new PooledObject<T>(PoolObject<T>.Get());
    }

    /// <summary>
    /// Tries to get an object from the pool wrapped in a PooledObject for automatic disposal.
    /// </summary>
    /// <typeparam name="T">The type of pooled object.</typeparam>
    /// <param name="pooled">The resulting PooledObject wrapper.</param>
    /// <returns>true if an object was retrieved; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetScoped<T>(out PooledObject<T> pooled) where T : PoolObject<T>, new()
    {
        if (PoolObject<T>.TryGet(out var item) && item != null)
        {
            pooled = new PooledObject<T>(item);
            return true;
        }
        pooled = default;
        return false;
    }
}