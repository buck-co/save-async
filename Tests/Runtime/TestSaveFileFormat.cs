using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buck.SaveAsync;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class TestSaveFileFormat
    {
        async Task<string> GetSerializedFileForObject(string key, object savedObject)
        {
            var fileName = Guid.NewGuid() + ".dat";
            
            var fileHandler = ScriptableObject.CreateInstance<InMemoryFileHandler>();
            fileHandler.AllOperationDelay = TimeSpan.Zero;
            
            var saveManagerGo = new GameObject();
            var saveManager = saveManagerGo.AddComponent<SaveManager>();
            SaveManagerReflectionExtensions.SetCustomFileHandler(fileHandler);

            var saveableEntity = new GameObject();
            var saveable = saveableEntity.AddComponent<TestSaveableEntity>();
            saveable.Key = key;
            saveable.Filename = fileName;
            saveable.CurrentState = savedObject;
            saveable.RegisterSelf();
            await SaveManager.Save(fileName);
            
            return await fileHandler.ReadFile(fileName, CancellationToken.None);
        }
        
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesNestedDictionary_SavesNestedJson() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var nestedObject = new Dictionary<string, object>
                {
                    { "key1", "value1" },
                    { "key2", 2 },
                    {
                        "key3", new Dictionary<string, object>
                        {
                            { "key4", "value4" },
                            { "key5", 5 }
                        }
                    }
                };
                
                // Act
                var key = Guid.NewGuid().ToString();
                var serializedFile = await GetSerializedFileForObject(key, nestedObject);
                
                // Assert
                var expected = $@"
[
  {{
    ""key"": ""{key}"",
    ""data"": {{
      ""key1"": ""value1"",
      ""key2"": 2,
      ""key3"": {{
        ""key4"": ""value4"",
        ""key5"": 5
      }}
    }}
  }}
]
";
                StringDiffUtils.AssertMultilineStringEqual(expected,serializedFile);
            });
        
        class TestSaveObject
        {
            public int IntValue { get; set; }
            public string StringValue { get; set; }
            public Vector3 Vector3Value { get; set; }
        }
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesObject_SavesJson() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var nestedObject = new TestSaveObject
                {
                    IntValue = 1337,
                    StringValue = "Goodbye, World!",
                    Vector3Value = new Vector3(1, 2, 3.5f)
                };
                
                // Act
                var key = Guid.NewGuid().ToString();
                var serializedFile = await GetSerializedFileForObject(key, nestedObject);
                
                // Assert
                var expected = $@"
[
  {{
    ""key"": ""{key}"",
    ""data"": {{
      ""intValue"": 1337,
      ""stringValue"": ""Goodbye, World!"",
      ""vector3Value"": {{x: 1, y: 2, z: 3.5}}
    }}
  }}
]
";
                StringDiffUtils.AssertMultilineStringEqual(expected,serializedFile);
            });

    }
}