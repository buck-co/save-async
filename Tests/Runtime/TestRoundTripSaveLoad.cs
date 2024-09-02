using System;
using System.Collections;
using System.Collections.Generic;
using Buck.SaveAsync;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class TestRoundTripSaveLoad
    {
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesState_AndChangesState_RestoresState() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var seed = Guid.NewGuid().ToString();
                
                var fileHandler = ScriptableObject.CreateInstance<InMemoryFileHandler>();
                fileHandler.AllOperationDelay = TimeSpan.Zero;
            
                var saveManagerGo = new GameObject();
                var saveManager = saveManagerGo.AddComponent<SaveManager>();
                SaveManagerReflectionExtensions.SetCustomFileHandler(fileHandler);

                var saveableEntity = new GameObject();
                var saveable = saveableEntity.AddComponent<TestSaveableEntity>();
                saveable.Key = "saveable_" + seed;
                saveable.Filename = "test.dat";
                saveable.CurrentState = "Hello, World!";
                saveable.RegisterSelf();
                await SaveManager.Save("test.dat");

                // Act
                saveable.CurrentState = "Goodbye, World!";
                await SaveManager.Load("test.dat");
            
                // Assert
                Assert.AreEqual("Hello, World!", saveable.CurrentState);
            });
        
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesState_AndChangesState_RestoresState_WithDelay() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var seed = Guid.NewGuid().ToString();
                
                var fileHandler = ScriptableObject.CreateInstance<InMemoryFileHandler>();
                fileHandler.AllOperationDelay = TimeSpan.FromSeconds(0.3f);
            
                var saveManagerGo = new GameObject();
                var saveManager = saveManagerGo.AddComponent<SaveManager>();
                SaveManagerReflectionExtensions.SetCustomFileHandler(fileHandler);

                var saveableEntity = new GameObject();
                var saveable = saveableEntity.AddComponent<TestSaveableEntity>();
                saveable.Key = "saveable_" + seed;
                saveable.Filename = "test.dat";
                saveable.CurrentState = "Hello, World!";
                saveable.RegisterSelf();
                await SaveManager.Save("test.dat");

                // Act
                saveable.CurrentState = "Goodbye, World!";
                await SaveManager.Load("test.dat");
            
                // Assert
                Assert.AreEqual("Hello, World!", saveable.CurrentState);
            });
    }
}