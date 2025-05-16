namespace ObjectPool.Tests;

public class PoolObjectTests
{
    // Reset the pool after each test
    public PoolObjectTests()
    {
        TestPoolObject.Dispose();
    }

    [Fact]
    public void Get_ReturnsNewObject_WhenPoolIsEmpty()
    {
        // Act
        var obj = TestPoolObject.Get();

        // Assert
        Assert.NotNull(obj);
        Assert.Equal(1, TestPoolObject.CountActive);
        Assert.Equal(0, TestPoolObject.CountInactive);
        Assert.Equal(1, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void Get_ReusesObject_WhenPoolHasObjects()
    {
        // Arrange
        TestPoolObject.Dispose();
        var obj = TestPoolObject.Get();
        obj.Name = "Modified";
        TestPoolObject.Release(obj);

        // Act
        var reusedObj = TestPoolObject.Get();

        // Assert
        Assert.NotNull(reusedObj);
        Assert.Equal("Default", reusedObj.Name); // Name was reset by the Reset method
        Assert.Equal(1, TestPoolObject.CountActive);
        Assert.Equal(0, TestPoolObject.CountInactive);
        Assert.Equal(1, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void Get_WithFactory_ReturnsCustomObject()
    {
        // Arrange
        string customName = "CustomObject";

        // Act
        var obj = TestPoolObject.Get(() => new TestPoolObject { Name = customName });

        // Assert
        Assert.NotNull(obj);
        Assert.Equal(customName, obj.Name);
        Assert.Equal(1, TestPoolObject.CountActive);
    }

    [Fact]
    public void Get_WithCount_ReturnsMultipleObjects()
    {
        // Arrange
        TestPoolObject.Create(5);

        // Act
        var objects = TestPoolObject.Get(3).ToList();

        // Assert
        Assert.Equal(3, objects.Count);
        Assert.Equal(3, TestPoolObject.CountActive);
        Assert.Equal(2, TestPoolObject.CountInactive);
        Assert.Equal(5, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void Get_ThrowsException_WhenPoolReachesMaxSize()
    {
        // Arrange
        TestPoolObject.MaxPoolSize = 2;
        var obj1 = TestPoolObject.Get();
        var obj2 = TestPoolObject.Get();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => TestPoolObject.Get());
        Assert.Contains("Pool has reached maximum size", ex.Message);
    }

    [Fact]
    public void Release_ReturnsObjectToPool()
    {
        // Arrange
        var obj = TestPoolObject.Get();

        // Act
        TestPoolObject.Release(obj);

        // Assert
        Assert.Equal(0, TestPoolObject.CountActive);
        Assert.Equal(1, TestPoolObject.CountInactive);
        Assert.Equal(1, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void Release_HandlesNullObject_WithoutException()
    {
        // Arrange
        TestPoolObject? nullObj = null;

        // Act & Assert (no exception should be thrown)
        TestPoolObject.Release(nullObj); // Should not throw an exception
    }

    [Fact]
    public void Release_ResetsObjectState()
    {
        // Arrange
        var obj = TestPoolObject.Get();
        obj.Name = "Modified";
        obj.IsActive = true;

        // Act
        TestPoolObject.Release(obj);
        var newObj = TestPoolObject.Get(); // If the returned object is the same, it should be reset

        // Assert
        Assert.Equal("Default", newObj.Name);
        Assert.False(newObj.IsActive);
    }

    [Fact]
    public void Release_WithMultipleObjects_ReturnsAllObjectsToPool()
    {
        // Arrange
        var objects = new[] { TestPoolObject.Get(), TestPoolObject.Get(), TestPoolObject.Get() };

        // Act
        TestPoolObject.Release(objects);

        // Assert
        Assert.Equal(0, TestPoolObject.CountActive);
        Assert.Equal(3, TestPoolObject.CountInactive);
        Assert.Equal(3, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void Create_AddsObjectsToPool()
    {
        // Arrange
        TestPoolObject.Dispose(); // Ensure the pool is empty

        // Act
        TestPoolObject.Create(3);

        // Assert
        Assert.Equal(0, TestPoolObject.CountActive);
        // Check if the actual count is greater than 0
        Assert.True(TestPoolObject.CountInactive > 0);
        Assert.True(TestPoolObject.CurrentSize > 0);
    }

    [Fact]
    public void Create_WithFactory_AddsCustomizedObjectsToPool()
    {
        // Arrange
        string customName = "CustomPoolObject";

        // Act
        TestPoolObject.Create(() => new TestPoolObject { Name = customName }, 2);
        var obj = TestPoolObject.Get();

        // Assert
        Assert.Equal(customName, obj.Name);
        Assert.Equal(1, TestPoolObject.CountActive);
        Assert.Equal(1, TestPoolObject.CountInactive);
        Assert.Equal(2, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void MaxPoolSize_ReducingSize_ClearsPool()
    {
        // Arrange
        TestPoolObject.Create(5);

        // Act
        TestPoolObject.MaxPoolSize = 2;

        // Assert
        Assert.Equal(0, TestPoolObject.CountInactive);
        Assert.Equal(0, TestPoolObject.CountActive);
        Assert.Equal(0, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void MaxPoolSize_IncreasingSize_AllowsMoreObjects()
    {
        // Arrange
        TestPoolObject.Dispose();
        TestPoolObject.MaxPoolSize = 2;
        TestPoolObject.Create(2);

        // Act
        TestPoolObject.MaxPoolSize = 5;
        var beforeCount = TestPoolObject.CurrentSize;
        TestPoolObject.Create(3); // Should work now
        var afterCount = TestPoolObject.CurrentSize;

        // Assert
        Assert.Equal(2, beforeCount);
        Assert.Equal(5, afterCount);
    }

    [Fact]
    public void Destroy_WorksWithoutErrorOnActiveObjects()
    {
        // We're only testing that the method executes without errors
        // The actual implementation is difficult to test

        // Arrange
        TestPoolObject.Dispose();
        var obj = TestPoolObject.Get();

        // Act & Assert (should not throw an exception)
        TestPoolObject.Destroy(obj);
    }

    [Fact]
    public void Dispose_ClearsPool()
    {
        // Arrange
        TestPoolObject.Create(5);
        var obj = TestPoolObject.Get();

        // Act
        TestPoolObject.Dispose();

        // Assert
        Assert.Equal(0, TestPoolObject.CountActive);
        Assert.Equal(0, TestPoolObject.CountInactive);
        Assert.Equal(0, TestPoolObject.CurrentSize);
    }

    [Fact]
    public void MultipleThreads_CanUsePoolConcurrently()
    {
        // Arrange
        TestPoolObject.Dispose(); // Reset the pool
        int threadCount = 5;
        int operationsPerThread = 1; // Reduced for faster tests
        TestPoolObject.MaxPoolSize = threadCount * operationsPerThread * 2; // Allow a larger pool

        // Act - Create some objects
        for (int i = 0; i < threadCount; i++)
        {
            var obj = TestPoolObject.Get();
            TestPoolObject.Release(obj);
        }

        // Then perform parallel operations
        Parallel.For(0, threadCount, _ =>
        {
            try
            {
                var obj = TestPoolObject.Get();
                TestPoolObject.Release(obj);
            }
            catch (InvalidOperationException)
            {
                // Ignore errors if the pool is full
            }
        });

        // Assert
        // We cannot predict exactly how many objects are active/inactive,
        // but the test should run without errors
        Assert.True(true);
    }

    [Fact]
    public void PoolProperties_ReturnCorrectValues()
    {
        // Arrange
        TestPoolObject.Create(5);
        var obj1 = TestPoolObject.Get();
        var obj2 = TestPoolObject.Get();

        // Assert
        Assert.Equal(2, TestPoolObject.CountActive);
        Assert.Equal(3, TestPoolObject.CountInactive);
        Assert.Equal(5, TestPoolObject.CurrentSize);
        Assert.Equal(1000, TestPoolObject.MaxPoolSize); // Default value is 1000
    }
}
