using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Buck.DataManagement
{
    public class DataManager : Singleton<DataManager>
    {
        [SerializeField] bool m_encryptData = true;
        
        [SerializeField, Tooltip("WARNING: Changing this field after deployment will break saves!")]
        string m_encryptionPassword = "password";

        [SerializeField] bool m_useBackgroundThreads = false;
        static HashSet<ISaveable> m_saveables = new();
        static Dictionary<string, ES3Settings> m_files = new();
        static Queue<string[]> m_saveQueue = new();
        static Queue<string[]> m_loadQueue = new();
        
        bool m_isSaving;
        bool m_isLoading;
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        System.Diagnostics.Stopwatch m_saveStopwatch = new ();
        System.Diagnostics.Stopwatch m_loadStopwatch = new ();
        #endif
        
        // TODOS:
        // - Turn this into a package
        // - Add the ability to swap out EasySave and use JSONUtility instead
        // - Add a way to erase files without deleting them from disk
        // - Add a way to delete save data for a specific ISaveable
        // - Add save versions and data migrations
        // - Create a debug visual that can be used for testing on devices
        // - Test on PlayStation, Xbox, Switch, iOS, Android

        #region Data Manager API
        
        /// <summary>
        /// Register an ISaveable object with the DataManager.
        /// </summary>
        public static void RegisterSaveable(ISaveable saveable)
        {
            // Store the saveable
            m_saveables.Add(saveable);

            // If we already have a file for this saveable, return
            if (m_files.ContainsKey(saveable.FileName)) return;
            
            // Otherwise, create an ES3 file for each file name
            ES3Settings location = new ES3Settings(saveable.FileName, ES3.Location.Cache);
            ES3Settings settings = new ES3Settings(saveable.FileName,
                Instance.m_encryptData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
                Instance.m_encryptionPassword,
                location);
            
            m_files.Add(saveable.FileName, settings);
        }

        // Async save method
        public async Awaitable SaveAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                // If these files are not in the queue, add them
                if (!m_saveQueue.Contains(filenames))
                    m_saveQueue.Enqueue(filenames);

                // If we are already saving or loading, return
                if (m_isSaving || m_isLoading)
                    return;

                m_isSaving = true;
                
                while (m_saveQueue.Count > 0)
                {
                    string[] filenamesToSave = m_saveQueue.Dequeue();
                    
                    // Switch to a background thread for caching the data
                    if (m_useBackgroundThreads)
                        await Awaitable.BackgroundThreadAsync();
                    
                    // Cache all ISaveable objects for each file
                    foreach (string filename in filenamesToSave)
                        foreach (ISaveable s in m_saveables)
                            if (s.FileName == filename)
                                ES3.Save(s.Guid.ToString(), s.CaptureState(), m_files[filename]);

                    // Switch back to the main thread for file I/O
                    await Awaitable.MainThreadAsync();
                        
                    // Store the file to disk
                    foreach (string filename in filenamesToSave)
                    {
                        if (m_files.TryGetValue(filename, out var file))
                            ES3.StoreCachedFile(file);
                        else
                            Debug.LogError("No file exists with the name \"" + filename + "\"" +
                                           " - Make sure you have registered the file with the DataManager.", this);
                    }
                }

                m_isSaving = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }

        // Async load method
        public async Awaitable LoadAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                // If these files are not in the queue, add them
                if (!m_loadQueue.Contains(filenames))
                    m_loadQueue.Enqueue(filenames);

                // If we are already saving or loading, return
                if (m_isSaving || m_isLoading)
                    return;

                m_isLoading = true;
                
                while (m_loadQueue.Count > 0)
                {
                    string[] filenamesToLoad = m_loadQueue.Dequeue();
                    
                    // Load the files from disk into the ES3 cache
                    foreach (string filename in filenamesToLoad)
                    {
                        if (m_files.TryGetValue(filename, out var file))
                            ES3.CacheFile(filename, file);
                        else
                            Debug.LogError("No file exists with the name \"" + filename + "\"" +
                                           " - Make sure you have registered the file with the DataManager.", this);
                    }
                    
                    // Switch to a background thread for loading the cached data
                    if (m_useBackgroundThreads)
                        await Awaitable.BackgroundThreadAsync();
                    
                    // Cache all ISaveable objects for each file
                    foreach (string filename in filenamesToLoad)
                        foreach (ISaveable s in m_saveables)
                            if (s.FileName == filename)
                                s.RestoreState(ES3.Load(s.Guid.ToString(), m_files[filename]));

                    // Switch back to the main thread after loading is complete
                    // await Awaitable.MainThreadAsync();
                }

                m_isLoading = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }
        
        #endregion

        #region Editor Tests
        
        [ButtonGroup("SaveGroup"), DisableInEditorMode]
        public async void SaveGameData()
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                m_saveStopwatch.Start();
                Debug.Log("Starting SaveAsync()...");
            #endif
            
            await SaveAsync(new[] { FileNames.GameData, FileNames.SomeFile });
            
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
            
            for(int i=0; i<100; i++)
                await SaveAsync(new[] { FileNames.GameData, FileNames.SomeFile });
                        
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("SaveAsync() completed in " + m_saveStopwatch.ElapsedMilliseconds + "ms");
                m_saveStopwatch.Stop();
                m_saveStopwatch.Reset();
            #endif
        }
        
        [ButtonGroup("LoadGroup"), DisableInEditorMode]
        public async void LoadGameData()
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                m_loadStopwatch.Start();
                Debug.Log("Starting LoadAsync()...");
            #endif
            
            await LoadAsync(new[] { FileNames.GameData, FileNames.SomeFile });
            
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
            
            for(int i=0; i<100; i++)
                await LoadAsync(new[] { FileNames.GameData, FileNames.SomeFile });
                        
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("LoadAsync() completed in " + m_loadStopwatch.ElapsedMilliseconds + "ms");
                m_loadStopwatch.Stop();
                m_loadStopwatch.Reset();
            #endif
        }
        
        #endregion
        
    }
}
