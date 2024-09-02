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