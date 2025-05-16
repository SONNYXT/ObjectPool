namespace ObjectPool.App;

public class TestModel : PoolObject<TestModel>
{

    public TestModel()
    {
        Id = Guid.NewGuid().ToString();
        Name = "TestModel";
        Description = "This is a test model.";
    }
    
    public TestModel(string id, string name, string description) 
    {
        Id = id;
        Name = name;
        Description = description;
    }
    
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    protected override void Reset()
    {
        Console.WriteLine("Reset");
    }
}