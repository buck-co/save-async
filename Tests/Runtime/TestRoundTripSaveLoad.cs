using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Buck.SaveAsync.Tests
{
    public class TestRoundTripSaveLoad : TestCaseBase
    {
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesStringState_AndChangesState_RestoresState() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var seed = Guid.NewGuid().ToString();
                
                var fileHandler = CreateFileHandler();
                SetupSaveManager(fileHandler);
                var saveable = CreateSaveableEntity("saveable_" + seed, "test.dat");
                
                saveable.CurrentState = "Hello, World!";
                await SaveManager.Save("test.dat");

                // Act
                saveable.CurrentState = "Goodbye, World!";
                await SaveManager.Load("test.dat");
            
                // Assert
                Assert.AreEqual("Hello, World!", saveable.CurrentState);
            });
        
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesStringState_AndChangesState_RestoresState_WithDelay() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var seed = Guid.NewGuid().ToString();
                
                var fileHandler = CreateFileHandler(TimeSpan.FromSeconds(0.3f));
                SetupSaveManager(fileHandler);
                var saveable = CreateSaveableEntity("saveable_" + seed, "test.dat");
                
                saveable.CurrentState = "Hello, World!";
                await SaveManager.Save("test.dat");

                // Act
                saveable.CurrentState = "Goodbye, World!";
                await SaveManager.Load("test.dat");
            
                // Assert
                Assert.AreEqual("Hello, World!", saveable.CurrentState);
            });
        
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesVector3State_AndChangesState_RestoresState() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var seed = Guid.NewGuid().ToString();
                
                var fileHandler = CreateFileHandler();
                SetupSaveManager(fileHandler);
                var saveable = CreateSaveableEntity("saveable_" + seed, "test.dat");
                
                var expected = new Vector3(1, 2.3f, 10000.2f);
                saveable.CurrentState = expected;
                await SaveManager.Save("test.dat");

                // Act
                saveable.CurrentState = Vector3.zero;
                await SaveManager.Load("test.dat");
            
                // Assert
                var actual = (Vector3)saveable.CurrentState;
                Assert.AreEqual(0, (expected - actual).magnitude, 0.0001f);
            });
    }
}