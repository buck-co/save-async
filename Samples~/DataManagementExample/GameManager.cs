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
        System.Diagnostics.Stopwatch m_deleteStopwatch = new();
#endif

        [ButtonGroup("SaveGroup"), DisableInEditorMode]
        public async void SaveGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_saveStopwatch.Start();
            Debug.Log("Starting SaveGameData()...");
#endif

            await DataManager.Instance.SaveAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("SaveGameData() completed in " + m_saveStopwatch.ElapsedMilliseconds + "ms");
            m_saveStopwatch.Stop();
            m_saveStopwatch.Reset();
#endif
        }

        [ButtonGroup("SaveGroup"), DisableInEditorMode]
        public async void SaveQueueTest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_saveStopwatch.Start();
            Debug.Log("Starting SaveQueueTest()...");
#endif

            for (int i = 0; i < 100; i++)
                await DataManager.Instance.SaveAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("SaveQueueTest() completed in " + m_saveStopwatch.ElapsedMilliseconds + "ms");
            m_saveStopwatch.Stop();
            m_saveStopwatch.Reset();
#endif
        }
        
        [ButtonGroup("EraseDeleteGroup"), DisableInEditorMode]
        public async void EraseGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_deleteStopwatch.Start();
            Debug.Log("Starting EraseGameData()...");
#endif

            await DataManager.Instance.EraseAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("EraseGameData() completed in " + m_deleteStopwatch.ElapsedMilliseconds + "ms");
            m_deleteStopwatch.Stop();
            m_deleteStopwatch.Reset();
#endif
        }
        
        [ButtonGroup("EraseDeleteGroup"), DisableInEditorMode]
        public async void DeleteGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_deleteStopwatch.Start();
            Debug.Log("Starting EraseGameData()...");
#endif

            await DataManager.Instance.DeleteAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("EraseGameData() completed in " + m_deleteStopwatch.ElapsedMilliseconds + "ms");
            m_deleteStopwatch.Stop();
            m_deleteStopwatch.Reset();
#endif
        }

        /*[ButtonGroup("LoadGroup"), DisableInEditorMode]
        public async void LoadGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_loadStopwatch.Start();
            Debug.Log("Starting LoadGameData()...");
#endif

            await LoadAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("LoadGameData() completed in " + m_loadStopwatch.ElapsedMilliseconds + "ms");
            m_loadStopwatch.Stop();
            m_loadStopwatch.Reset();
#endif
        }

        [ButtonGroup("LoadGroup"), DisableInEditorMode]
        public async void LoadQueueTest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_loadStopwatch.Start();
            Debug.Log("Starting LoadQueueTest()...");
#endif

            for (int i = 0; i < 100; i++)
                await LoadAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("LoadQueueTest() completed in " + m_loadStopwatch.ElapsedMilliseconds + "ms");
            m_loadStopwatch.Stop();
            m_loadStopwatch.Reset();
#endif
        }*/
    }

}
