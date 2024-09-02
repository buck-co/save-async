using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Buck.SaveAsync.Tests
{
    public class TestCaseBase
    {
        protected virtual void SetupSaveManager(FileHandler withFileHandler)
        {
            ReflectionExtensions.SetCustomFileHandler(withFileHandler);
            // ensure that the default settings are not overriden, for test consistency.
            JsonConvert.DefaultSettings = null;
        }

        protected FileHandler CreateFileHandler(TimeSpan? withEmulatedDelay = null)
        {
            var fileHandler = ScriptableObject.CreateInstance<InMemoryFileHandler>();
            fileHandler.AllOperationDelay = withEmulatedDelay ?? TimeSpan.Zero;
            return fileHandler;
        }
        
        public TestSaveableEntity CreateSaveableEntity(string key, string filename = "test.dat")
        {
            var saveableEntity = new GameObject();
            var saveable = saveableEntity.AddComponent<TestSaveableEntity>();
            saveable.Key = key;
            saveable.Filename = filename;
            saveable.RegisterSelf();
            return saveable;
        }

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
    }
}