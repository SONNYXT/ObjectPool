using ObjectPool.App;

// Initialize the pool with 5 objects
Console.WriteLine("=== Pool Initialization ===");
TestModel.Create(5);

// Test 1: Basic pool operations
Console.WriteLine("\n=== Test 1: Basic Pool Operations ===");
var poolObject = TestModel.Get(() => new TestModel("223-10-01", "TestModel1", "This is a test model."));

Console.WriteLine("\nObject Details:");
Console.WriteLine($"Name: {poolObject?.Name}");
Console.WriteLine($"Id: {poolObject?.Id}");
Console.WriteLine($"Description: {poolObject?.Description}");
Console.WriteLine("\nPool Status:");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");

Console.WriteLine("\n=== Test 2: Object Release and Reuse ===");
TestModel.Release(poolObject);
poolObject = TestModel.Get();
poolObject.Name = "TestModel2";
Console.WriteLine($"Name: {poolObject.Name}");
Console.WriteLine($"Id: {poolObject.Id}");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");

Console.WriteLine("\n=== Test 3: Pool Reset ===");
TestModel.Dispose();

Console.WriteLine("\n=== Test 4: New Object After Dispose ===");
var poolObject2 = TestModel.Get();
poolObject2.Name = "TestModel3";
Console.WriteLine($"Name: {poolObject2.Name}");
Console.WriteLine($"Id: {poolObject2.Id}");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");

var poolObject3 = TestModel.Get();
poolObject3.Name = "TestModel4";
Console.WriteLine($"Name: {poolObject3.Name}");
Console.WriteLine($"Id: {poolObject3.Id}");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");

TestModel.MaxPoolSize = 250;
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");

poolObject3 = TestModel.Get();
poolObject3.Name = "TestModel5";
Console.WriteLine($"Name: {poolObject3.Name}");
Console.WriteLine($"Id: {poolObject3.Id}");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
    
var poolObject4 = TestModel.Get();
poolObject4.Name = "TestModel6";
Console.WriteLine($"Name: {poolObject4.Name}");
Console.WriteLine($"Id: {poolObject4.Id}");

Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");
TestModel.Release(poolObject4);
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");
poolObject4 = TestModel.Get();
poolObject4.Name = "TestModel7";
Console.WriteLine($"Name: {poolObject4.Name}");
Console.WriteLine($"Id: {poolObject4.Id}");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");

