namespace ObjectPool.App;

public class TestModel(string id, string name, string description) : PoolObject<TestModel>
{
    public TestModel() : this(Guid.NewGuid().ToString(), "TestModel", "This is a test model.")
    {
    }

    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string Description { get; set; } = description;

    protected override void Reset()
    {
        Console.WriteLine("Resetting TestModel object");
        Name = "TestModel";
        Description = "This is a test model.";
        // ID remains unchanged as it's a unique identifier
    }
}

