using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Buck.SaveAsync.Tests
{
    /// <summary>
    /// These tests verify round-trip save and load by saving a state, changing the state,
    /// and then loading the saved state.
    /// </summary>
    /// <remarks>
    /// specifically meant for testing integration with Json-for-Unity converters
    /// </remarks>
    public class TestRoundTripSaveLoadWithUnityConverters : UnityConverterTestCaseBase
    {
        class SaveObjectWithNestedVector3
        {
            public Vector3 NestedVector3 { get; set; }
        }

        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesVector3StateInNestedProperty_AndChangesState_RestoresState() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                var expected = new Vector3(1, 2.3f, 10000.2f);
                var actual = await GetRoundTrip(new SaveObjectWithNestedVector3
                    {
                        NestedVector3 = expected
                    },
                    new SaveObjectWithNestedVector3
                    {
                        NestedVector3 = Vector3.zero
                    });
                Assert.AreEqual(0, (expected - actual.NestedVector3).magnitude, 0.0001f);
            });
        
        [UnityTest]
        public IEnumerator TestSaveSystem_WhenSavesVector3State_AndChangesState_RestoresState() 
            => AsyncToCoroutine.AsCoroutine(async () => 
            {
                var expected = new Vector3(1, 2.3f, 10000.2f);
                var actual = await GetRoundTrip(expected, Vector3.zero);
                Assert.AreEqual(0, (expected - actual).magnitude, 0.0001f);
            });
    }
}