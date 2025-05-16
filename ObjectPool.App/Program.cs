using ObjectPool.App;

// Initialisierung des Pools mit 5 Objekten
Console.WriteLine("=== Pool Initialisierung ===");
TestModel.Create(5);

// Test 1: Grundlegende Pool-Operationen
Console.WriteLine("\n=== Test 1: Basis Pool-Operationen ===");
var poolObject = TestModel.Get(() => new TestModel("223-10-01", "TestModel1", "Dies ist ein Testmodell."));
poolObject.Name = "TestModel1";

Console.WriteLine("\nObjekt Details:");
Console.WriteLine($"Name: {poolObject.Name}");
Console.WriteLine($"Id: {poolObject.Id}");
Console.WriteLine($"Description: {poolObject.Description}");
Console.WriteLine("\nPool Status:");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");

Console.WriteLine("\n=== Test 2: Objekt Freigabe und Wiederverwendung ===");
TestModel.Release(poolObject);
poolObject = TestModel.Get();
poolObject.Name = "TestModel2";
Console.WriteLine($"Name: {poolObject.Name}");
Console.WriteLine($"Id: {poolObject.Id}");
Console.WriteLine($"Count Active: {TestModel.CountActive}");
Console.WriteLine($"Count Inactive: {TestModel.CountInactive}");
Console.WriteLine($"Current Size: {TestModel.CurrentSize}");
Console.WriteLine($"Max Pool Size: {TestModel.MaxPoolSize}");

Console.WriteLine("\n=== Test 3: Pool zurücksetzen ===");
TestModel.Dispose();

Console.WriteLine("\n=== Test 4: Neues Objekt nach Dispose ===");
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

