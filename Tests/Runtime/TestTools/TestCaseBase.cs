using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Buck.SaveAsync.Tests
{
    /// <summary>
    /// A set of base methods used by most test cases. Overridable to provide different save manager setup methods,
    /// useful when we want to configure JsonConvert's default settings for example.
    /// </summary>
    public class TestCaseBase
    {
        protected virtual void SetupSaveManager(FileHandler withFileHandler)
        {
            SaveManagerExtensions.SetCustomFileHandler(withFileHandler);
            // ensure that the default settings are not overriden, for test consistency.
            JsonConvert.DefaultSettings = null;
        }

        protected FileHandler CreateFileHandler(TimeSpan? withEmulatedDelay = null)
        {
            var fileHandler = ScriptableObject.CreateInstance<InMemoryFileHandler>();
            fileHandler.AllOperationDelay = withEmulatedDelay ?? TimeSpan.Zero;
            return fileHandler;
        }
        
        protected TestSaveableEntity CreateSaveableEntity(string key, string filename = "test.dat")
        {
            var saveableEntity = new GameObject();
            var saveable = saveableEntity.AddComponent<TestSaveableEntity>();
            saveable.Key = key;
            saveable.Filename = filename;
            saveable.RegisterSelf();
            return saveable;
        }

        /// <summary>
        /// Creates a saveable object, saves it to an emulated file handler, and returns
        /// the serialized string from the emulated file handler.
        /// </summary>
        /// <param name="key">The key of the saveable object</param>
        /// <param name="savedObject">The data to place inside the saveable object</param>
        /// <returns>The string value which is saved to file when the temporary saveable object is saved</returns>
        protected async Task<string> GetSerializedFileForObject(string key, object savedObject)
        {
            var fileName = Guid.NewGuid() + ".dat";

            var fileHandler = CreateFileHandler();
            SetupSaveManager(fileHandler);

            var saveable = CreateSaveableEntity(key, fileName);
            saveable.CurrentState = savedObject;
            await SaveManager.Save(fileName);
            
            return await fileHandler.ReadFile(fileName, CancellationToken.None);
        }

        /// <summary>
        /// Tests round-trip serialization of a given value.
        /// <paramref name="initial"/> is round-tripped through the save system backed by a temporary file handler.
        /// </summary>
        /// <param name="initial">The value to be round-tripped</param>
        /// <param name="resetTo">A value used to clear the saveable's internal state, to ensure the round-trip isn't a result of a cached value.</param>
        /// <param name="fileHandlerDelay">Optional artificial delay applied to the file handler</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>The value in the saveable after it has been Loaded</returns>
        protected async Task<T> GetRoundTrip<T>(T initial, T resetTo, TimeSpan? fileHandlerDelay = null)
        {
            // Arrange
            var seed = Guid.NewGuid().ToString();
                
            var fileHandler = CreateFileHandler(fileHandlerDelay);
            SetupSaveManager(fileHandler);
            var saveable = CreateSaveableEntity("saveable_" + seed, "test.dat");
                
            saveable.CurrentState = initial;
            await SaveManager.Save("test.dat");

            // Act
            saveable.CurrentState = resetTo;
            await SaveManager.Load("test.dat");
            
            // Assert
            Assert.IsAssignableFrom(typeof(T), saveable.CurrentState);
            return (T)saveable.CurrentState;
        }
    }
}