using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Buck.SaveAsync.Tests
{
    internal class TestUnitySaveObject
    {
        public Vector3 Vector3Value { get; set; }
        public Quaternion QuaternionValue { get; set; }
        public Color ColorValue { get; set; }
        public AnimationCurve AnimationCurveValue { get; set; }
    }
    
    /// <summary>
    /// These tests require that Newtonsoft.Json-for-Unity.Converters are installed.
    /// They verify that the save system correctly integrates with Json-for-Unity.
    /// </summary>
    /// <remarks>
    /// install from https://github.com/applejag/Newtonsoft.Json-for-Unity.Converters
    /// </remarks>
    public class TestSaveFileFormatWithUnityConverters : UnityConverterTestCaseBase
    {
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesUnityObjects_SavesJson() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                // Arrange
                var nestedObject = new TestUnitySaveObject
                {
                    Vector3Value = new Vector3(1, 2, 3.5f),
                    QuaternionValue = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f),
                    ColorValue = new Color(0.1f, 0.2f, 0.3f, 0.4f),
                    AnimationCurveValue = AnimationCurve.EaseInOut(0, 0, 1, 1)
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
      ""$type"": ""{TestConstants.Namespace}.TestUnitySaveObject, {TestConstants.Assembly}.UnityConverters"",
      ""Vector3Value"": {{
        ""x"": 1.0,
        ""y"": 2.0,
        ""z"": 3.5
      }},
      ""QuaternionValue"": {{
        ""x"": 0.1,
        ""y"": 0.2,
        ""z"": 0.3,
        ""w"": 0.4
      }},
      ""ColorValue"": {{
        ""r"": 0.1,
        ""g"": 0.2,
        ""b"": 0.3,
        ""a"": 0.4
      }},
      ""AnimationCurveValue"": {{
        ""keys"": [
          {{
            ""time"": 0.0,
            ""value"": 0.0,
            ""inTangent"": 0.0,
            ""outTangent"": 0.0,
            ""inWeight"": 0.0,
            ""outWeight"": 0.0,
            ""weightedMode"": ""None"",
            ""tangentMode"": 0
          }},
          {{
            ""time"": 1.0,
            ""value"": 1.0,
            ""inTangent"": 0.0,
            ""outTangent"": 0.0,
            ""inWeight"": 0.0,
            ""outWeight"": 0.0,
            ""weightedMode"": ""None"",
            ""tangentMode"": 0
          }}
        ],
        ""length"": 2,
        ""preWrapMode"": ""ClampForever"",
        ""postWrapMode"": ""ClampForever""
      }}
    }}
  }}
]
";
                StringDiffUtils.AssertMultilineStringEqual(expected,serializedFile);
            });
    }
}