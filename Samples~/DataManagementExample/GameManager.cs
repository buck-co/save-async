using UnityEngine;
using Sirenix.OdinInspector;
using Buck.DataManagement;

namespace Buck.DataManagementExample
{
    public class GameManager : MonoBehaviour
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        System.Diagnostics.Stopwatch m_saveStopwatch = new();
        System.Diagnostics.Stopwatch m_loadStopwatch = new();
#endif

        #region Editor Tests

        [ButtonGroup("SaveGroup"), DisableInEditorMode]
        public async void SaveGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_saveStopwatch.Start();
            Debug.Log("Starting SaveAsync()...");
#endif

            await DataManager.Instance.SaveAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("SaveAsync() completed in " + m_saveStopwatch.ElapsedMilliseconds + "ms");
            m_saveStopwatch.Stop();
            m_saveStopwatch.Reset();
#endif
        }

        [ButtonGroup("SaveGroup"), DisableInEditorMode]
        public async void SaveQueueTest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_saveStopwatch.Start();
            Debug.Log("Starting SaveAsync()...");
#endif

            for (int i = 0; i < 100; i++)
                await DataManager.Instance.SaveAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("SaveAsync() completed in " + m_saveStopwatch.ElapsedMilliseconds + "ms");
            m_saveStopwatch.Stop();
            m_saveStopwatch.Reset();
#endif
        }

        /*[ButtonGroup("LoadGroup"), DisableInEditorMode]
        public async void LoadGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_loadStopwatch.Start();
            Debug.Log("Starting LoadAsync()...");
#endif

            await LoadAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("LoadAsync() completed in " + m_loadStopwatch.ElapsedMilliseconds + "ms");
            m_loadStopwatch.Stop();
            m_loadStopwatch.Reset();
#endif
        }

        [ButtonGroup("LoadGroup"), DisableInEditorMode]
        public async void LoadQueueTest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_loadStopwatch.Start();
            Debug.Log("Starting LoadAsync()...");
#endif

            for (int i = 0; i < 100; i++)
                await LoadAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("LoadAsync() completed in " + m_loadStopwatch.ElapsedMilliseconds + "ms");
            m_loadStopwatch.Stop();
            m_loadStopwatch.Reset();
#endif
        }*/

        #endregion
    }

}
