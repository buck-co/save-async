using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Buck.SaveAsync.Tests
{
    /// <summary>
    /// These tests verify round-trip save and load by saving a state, changing the state,
    /// and then loading the previously saved state.
    /// </summary>
    public class TestRoundTripSaveLoad : TestCaseBase
    {
        [UnityTest]
        public IEnumerator Test_RoundTrip_String()
        {
            async Awaitable Impl()
            {
                var expected = "Hello, World!";
                var actual = await GetRoundTrip(expected, "Goodbye, World!");
                
                Assert.AreEqual(expected, actual);
            }

            return Impl();
        }
        
        [UnityTest]
        public IEnumerator Test_RoundTrip_String_WithDelay()
        {
            async Awaitable Impl()
            {
                var expected = "Hello, World!";
                var actual = await GetRoundTrip(expected, "Goodbye, World!", TimeSpan.FromSeconds(0.3f));
                Assert.AreEqual(expected, actual);
            }

            return Impl();
        }
        
        class SaveObjectWithNestedVector3
        {
            public Vector3 NestedVector3 { get; set; }
        }
        
        [UnityTest]
        public IEnumerator Test_RoundTrip_Vector3Nested()
        {
            async Awaitable Impl()
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
            }

            return Impl();
        }
        
        [UnityTest]
        public IEnumerator Test_RoundTrip_Vector3Raw()
        {
            async Awaitable Impl()
            {
                var expected = new Vector3(1, 2.3f, 10000.2f);
                var actual = await GetRoundTrip(expected, Vector3.zero);
                Assert.AreEqual(0, (expected - actual).magnitude, 0.0001f);
            }

            return Impl();
        }
    }
}