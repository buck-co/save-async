using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Buck.DataManagement
{
    public class DataManager : Singleton<DataManager>
    {
        static HashSet<ISaveable> m_saveables = new();
        static Dictionary<string, List<ISaveable>> m_saveablesToSave = new();
        static HashSet<string> m_files = new();
        static Queue<string[]> m_saveQueue = new();
        static Queue<string[]> m_loadQueue = new();
        
        bool m_isSaving;
        bool m_isLoading;
        
        // TODOS:
        // [X] Turn this into a package
        // [ ] Add the ability to swap out EasySave and use JSONUtility instead
        // [ ] Add a way to erase files without deleting them from disk
        // [ ] Add a way to delete save data for a specific ISaveable
        // [ ] Explore the possibility of using a static class instead of a Singleton MonoBehaviour
        // [ ] Add save versions and data migrations
        // [ ] Create a debug visual that can be used for testing on devices
        // [ ] Test on PlayStation, Xbox, Switch, iOS, Android

        #region Data Manager API

        void Awake()
        {
            FileHandler.Initialize();
        }
        
        /// <summary>
        /// Register an ISaveable object with the DataManager.
        /// </summary>
        public static void RegisterSaveable(ISaveable saveable)
        {
            // Store the saveable
            m_saveables.Add(saveable);

            // Store the filename
            m_files.Add(saveable.FileName);
        }

        // Async save method
        public async Awaitable SaveAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                // Switch to a background thread
                await Awaitable.BackgroundThreadAsync();
                
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
                    
                    m_saveablesToSave.Clear();
                    
                    // Organize all of the ISaveables by filename into a dictionary of lists
                    foreach (string filename in filenamesToSave)
                        foreach (ISaveable s in m_saveables)
                            if (s.FileName == filename)
                            {
                                // Add the filename key to the dictionary and create its list if it doesn't exist
                                if (!m_saveablesToSave.ContainsKey(filename))
                                    m_saveablesToSave.Add(filename, new List<ISaveable>());

                                // Add the ISaveable to the filename's list
                                m_saveablesToSave[filename].Add(s);
                            }
                        
                    // Create a JSON string of each file's contents
                    foreach (KeyValuePair<string, List<ISaveable>> pair in m_saveablesToSave)
                    {
                        // Create a JSON string of the saveables
                        string json = JsonHelper.ToJson(pair.Value.ToArray(), true);

                        // Save the JSON string to disk
                        await FileHandler.WriteFile(pair.Key, json);
                    }
                }

                m_isSaving = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }

        // Async load method
        /*public async Awaitable LoadAsync(string[] filenames)
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
        }*/
        
        #endregion

        #region Data Manager Internal Methods
        
        async Awaitable WriteFile(string path, string content)
        {
            // If the cancellation token has been requested at any point, return
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                // Switch to a background thread for writing the file
                await Awaitable.BackgroundThreadAsync();

                FileStream fileStream = new FileStream(path, FileMode.Create);
                
                await using (StreamWriter writer = new StreamWriter(fileStream))
                    await writer.WriteAsync(content);
                
                // Switch back to the main thread after writing is complete
                await Awaitable.MainThreadAsync();

                return;
            }
        }
        
        #endregion
        
        
        
    }
}
