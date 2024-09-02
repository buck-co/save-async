using UnityEngine;

namespace Buck.SaveAsync.Tests
{
    /// <summary>
    /// A test MonoBehavior that implements saveable. Its internals are exposed since it is piloted
    /// from test cases.
    /// </summary>
    public class TestSaveableEntity : MonoBehaviour, ISaveable
    {
        public string Key { get; set; } = nameof(TestSaveableEntity);
        public string Filename { get; set; }
        public object CurrentState { get; set; }
        public object CaptureState() => CurrentState;
        public void RestoreState(object state) => this.CurrentState = state;

        public void RegisterSelf()
        {
            SaveManager.RegisterSaveable(this);
        }
    }
}