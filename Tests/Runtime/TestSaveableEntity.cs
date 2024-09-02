using System;
using Buck.SaveAsync;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// A test monobehavior that implements saveable. Its internals are exposed, so it can be piloted
    /// from a test case.
    /// </summary>
    public class TestSaveableEntity : MonoBehaviour, ISaveable
    {
        public string Key { get; set; } = nameof(TestSaveableEntity);
        public string Filename { get; set; }
        public object CurrentState { get; set; }
        public object CaptureState() => CurrentState;
        public void RestoreState(object state) => this.CurrentState = state;

        void Awake()
        {
            SaveManager.RegisterSaveable(this);
        }
    }
}