namespace ObjectPool.Tests;

public class TestPoolObject : PoolObject<TestPoolObject>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Default";
    public bool IsActive { get; set; } = false;

    protected override void Reset()
    {
        Name = "Default";
        IsActive = false;
    }
}
