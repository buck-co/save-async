using UnityEngine;
using Sirenix.OdinInspector;
using Buck.DataManagement;

namespace Buck.DataManagementExample
{
    public class GameManager : MonoBehaviour
    {
        [ButtonGroup("SaveGroup"), DisableInEditorMode]
        public async void SaveGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            Debug.Log("Starting SaveGameData()...");
#endif

            await DataManager.SaveAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("SaveGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Stop();
            stopwatch.Reset();
#endif
        }

        [ButtonGroup("SaveGroup"), DisableInEditorMode]
        public async void SaveQueueTest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            Debug.Log("Starting SaveQueueTest()...");
#endif

            for (int i = 0; i < 100; i++)
                await DataManager.SaveAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("SaveQueueTest() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Stop();
            stopwatch.Reset();
#endif
        }
        
        [ButtonGroup("EraseDeleteGroup"), DisableInEditorMode]
        public async void EraseGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            Debug.Log("Starting EraseGameData()...");
#endif

            await DataManager.EraseAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("EraseGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Stop();
            stopwatch.Reset();
#endif
        }
        
        [ButtonGroup("EraseDeleteGroup"), DisableInEditorMode]
        public async void DeleteGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            Debug.Log("Starting DeleteGameData()...");
#endif

            await DataManager.DeleteAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("DeleteGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Stop();
            stopwatch.Reset();
#endif
        }

        [ButtonGroup("LoadGroup"), DisableInEditorMode]
        public async void LoadGameData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            Debug.Log("Starting LoadGameData()...");
#endif

            await DataManager.LoadAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("LoadGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Stop();
            stopwatch.Reset();
#endif
        }

        [ButtonGroup("LoadGroup"), DisableInEditorMode]
        public async void LoadQueueTest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            Debug.Log("Starting LoadQueueTest()...");
#endif

            for (int i = 0; i < 100; i++)
                await DataManager.LoadAsync(new[] { Files.GameData, Files.SomeFile });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("LoadQueueTest() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Stop();
            stopwatch.Reset();
#endif
        }
    }

}
