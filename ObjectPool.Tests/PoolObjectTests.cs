using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ObjectPool.Tests
{
    [TestClass]
    public class PoolObjectTests
    {
        [TestInitialize]
        public void Initialize()
        {
            // Bereite jeden Test vor
            TestPoolObject.Dispose();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Aufräumen nach jedem Test
            TestPoolObject.Dispose();
        }

        [TestMethod]
        public void MaxPoolSize_ShouldSetAndGetCorrectly()
        {
            // Arrange
            int expectedMaxSize = 500;

            // Act
            TestPoolObject.MaxPoolSize = expectedMaxSize;
            int actualMaxSize = TestPoolObject.MaxPoolSize;

            // Assert
            Assert.AreEqual(expectedMaxSize, actualMaxSize);
        }

        [TestMethod]
        public void MaxPoolSize_WhenReducedBelowCurrentSize_ShouldClearPool()
        {
            // Arrange
            TestPoolObject.MaxPoolSize = 10;
            TestPoolObject.Create(5); // Erstelle 5 Objekte im Pool

            // Act
            TestPoolObject.MaxPoolSize = 3; // Reduziere max. Größe unter aktuelle Größe

            // Assert
            Assert.AreEqual(0, TestPoolObject.CurrentSize);
            Assert.AreEqual(0, TestPoolObject.CountInactive);
        }

        [TestMethod]
        public void Get_ShouldReturnObject()
        {
            // Act
            var obj = TestPoolObject.Get();

            // Assert
            Assert.IsNotNull(obj);
            Assert.AreEqual(1, TestPoolObject.CountActive);
            Assert.AreEqual(0, TestPoolObject.CountInactive);
            Assert.AreEqual(1, TestPoolObject.CurrentSize);
        }

        [TestMethod]
        public void Get_WithFactory_ShouldReturnObjectFromFactory()
        {
            // Arrange
            string expectedName = "CustomFactoryName";

            // Act
            var obj = TestPoolObject.Get(() => new TestPoolObject { Name = expectedName });

            // Assert
            Assert.IsNotNull(obj);
            Assert.AreEqual(expectedName, obj.Name);
            Assert.AreEqual(1, TestPoolObject.CountActive);
        }

        [TestMethod]
        public void Get_Count_ShouldReturnMultipleObjects()
        {
            // Arrange
            TestPoolObject.Create(5); // Erstelle 5 Objekte im Pool
            TestPoolObject.Release(TestPoolObject.Get()); // Bringe ein Objekt zurück in den Pool

            // Act
            var objects = TestPoolObject.Get(3).ToList();

            // Assert
            Assert.AreEqual(3, objects.Count);
            Assert.AreEqual(3, TestPoolObject.CountActive);
            Assert.AreEqual(3, TestPoolObject.CurrentSize);
        }

        [TestMethod]
        public void Get_WhenPoolIsFull_ShouldThrowException()
        {
            // Arrange
            TestPoolObject.MaxPoolSize = 1;
            var obj = TestPoolObject.Get();

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => TestPoolObject.Get());
        }

        [TestMethod]
        public void Create_ShouldAddObjectsToPool()
        {
            // Act
            TestPoolObject.Create(3);

            // Assert
            Assert.AreEqual(3, TestPoolObject.CountInactive);
            Assert.AreEqual(0, TestPoolObject.CountActive);
            Assert.AreEqual(3, TestPoolObject.CurrentSize);
        }

        [TestMethod]
        public void Create_BeyondMaxSize_ShouldLimitCreation()
        {
            // Arrange
            TestPoolObject.MaxPoolSize = 5;

            // Act
            TestPoolObject.Create(10); // Versuche 10 zu erstellen, aber max ist 5

            // Assert
            Assert.AreEqual(5, TestPoolObject.CountInactive);
            Assert.AreEqual(0, TestPoolObject.CountActive);
            Assert.AreEqual(5, TestPoolObject.CurrentSize);
        }

        [TestMethod]
        public void Release_ShouldReturnObjectToPool()
        {
            // Arrange
            var obj = TestPoolObject.Get();
            obj.Name = "Modified";

            // Act
            TestPoolObject.Release(obj);

            // Assert
            Assert.AreEqual(0, TestPoolObject.CountActive);
            Assert.AreEqual(1, TestPoolObject.CountInactive);
            
            // Stelle sicher, dass das Reset aufgerufen wurde
            var retrievedObj = TestPoolObject.Get();
            Assert.AreEqual("TestObject", retrievedObj.Name); // Sollte durch Reset zurückgesetzt sein
        }

        [TestMethod]
        public void Release_MultipleObjects_ShouldReturnAllToPool()
        {
            // Arrange
            var objects = new List<TestPoolObject>
            {
                TestPoolObject.Get(),
                TestPoolObject.Get(),
                TestPoolObject.Get()
            };

            // Act
            TestPoolObject.Release(objects);

            // Assert
            Assert.AreEqual(0, TestPoolObject.CountActive);
            Assert.AreEqual(3, TestPoolObject.CountInactive);
        }

        [TestMethod]
        public void Dispose_ShouldClearPool()
        {
            // Arrange
            TestPoolObject.Create(5);
            var active = TestPoolObject.Get();

            // Act
            TestPoolObject.Dispose();

            // Assert
            Assert.AreEqual(0, TestPoolObject.CountActive);
            Assert.AreEqual(0, TestPoolObject.CountInactive);
            Assert.AreEqual(0, TestPoolObject.CurrentSize);
        }

        [TestMethod]
        public void CountActive_ShouldReflectActiveObjects()
        {
            // Arrange
            TestPoolObject.Create(5);
            
            // Act
            var obj1 = TestPoolObject.Get();
            var obj2 = TestPoolObject.Get();
            
            // Assert
            Assert.AreEqual(2, TestPoolObject.CountActive);
        }

        [TestMethod]
        public void CountInactive_ShouldReflectInactiveObjects()
        {
            // Arrange
            TestPoolObject.Create(5);
            
            // Act
            var obj = TestPoolObject.Get();
            
            // Assert
            Assert.AreEqual(4, TestPoolObject.CountInactive);
        }

        [TestMethod]
        public void CurrentSize_ShouldReflectTotalManagedObjects()
        {
            // Arrange
            TestPoolObject.Create(3);
            
            // Act
            var obj1 = TestPoolObject.Get();
            var obj2 = TestPoolObject.Get();
            TestPoolObject.Release(obj1);
            
            // Assert
            Assert.AreEqual(3, TestPoolObject.CurrentSize);
        }

        [TestMethod]
        public void MultithreadAccess_ShouldHandleConcurrency()
        {
            // Arrange
            TestPoolObject.MaxPoolSize = 100;
            TestPoolObject.Create(10);
            
            // Act
            Parallel.For(0, 50, i =>
            {
                if (i % 2 == 0)
                {
                    var obj = TestPoolObject.Get();
                    TestPoolObject.Release(obj);
                }
                else
                {
                    TestPoolObject.Create(1);
                }
            });
            
            // Assert - Stellen wir sicher, dass keine Ausnahmen geworfen wurden
            Assert.IsTrue(TestPoolObject.CurrentSize <= 100);
        }
    }
}
