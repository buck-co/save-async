using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buck.SaveAsync;
using UnityEngine;
using UnityEngine.TestTools;

namespace Buck.SaveAsync.Tests
{
    internal class TestSaveObject
    {
        public int IntValue { get; set; }
        public string StringValue { get; set; }
    }
    
    public class TestSaveFileFormat : TestCaseBase
    {
        async Task<string> GetSerializedFileForObject(string key, object savedObject)
        {
            var fileName = Guid.NewGuid() + ".dat";

            var fileHandler = CreateFileHandler();
            SetupSaveManager(fileHandler);

            var saveable = CreateSaveableEntity(key, fileName);
            saveable.CurrentState = savedObject;
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
    ""Key"": ""{key}"",
    ""Data"": {{
      ""$type"": ""System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.Object, mscorlib]], mscorlib"",
      ""key1"": ""value1"",
      ""key2"": 2,
      ""key3"": {{
        ""$type"": ""System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.Object, mscorlib]], mscorlib"",
        ""key4"": ""value4"",
        ""key5"": 5
      }}
    }}
  }}
]
";
                StringDiffUtils.AssertMultilineStringEqual(expected,serializedFile);
            });
        
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesObject_SavesJson() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var nestedObject = new TestSaveObject
                {
                    IntValue = 1337,
                    StringValue = "Goodbye, World!"
                };
                
                // Act
                var key = Guid.NewGuid().ToString();
                var serializedFile = await GetSerializedFileForObject(key, nestedObject);
                
                // Assert
                var expected = $@"
[
  {{
    ""Key"": ""{key}"",
    ""Data"": {{
      ""$type"": ""{TestConstants.Namespace}.TestSaveObject, {TestConstants.Assembly}"",
      ""IntValue"": 1337,
      ""StringValue"": ""Goodbye, World!""
    }}
  }}
]
";
                StringDiffUtils.AssertMultilineStringEqual(expected,serializedFile);
            });
    }
}