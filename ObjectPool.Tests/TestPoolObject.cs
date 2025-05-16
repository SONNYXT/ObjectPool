using System;
using ObjectPool;

namespace ObjectPool.Tests
{
    public class TestPoolObject : PoolObject<TestPoolObject>
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "TestObject";
        
        public bool IsDisposed { get; private set; }
        
        public void Dispose()
        {
            IsDisposed = true;
        }
        
        protected override void Reset()
        {
            Name = "TestObject";
            IsDisposed = false;
        }
    }
}
