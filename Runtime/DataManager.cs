using System.Collections.Generic;
using UnityEngine;

namespace Buck.DataManagement
{
    public class DataManager : Singleton<DataManager>
    {
        [SerializeField] bool m_useEncryption = false;
        [SerializeField] string m_encryptionPassword = "password";
        static FileHandler m_fileHandler;
        static HashSet<ISaveable> m_saveables = new();
        static HashSet<string> m_files = new();
        static Queue<string[]> m_saveQueue = new();
        static Queue<string[]> m_loadQueue = new();
        static Queue<string[]> m_deleteQueue = new();
        
        // Dictionary with the filename as the key and a list of SaveObjects as the value
        Dictionary<string, List<SaveObject>> m_saveObjects = new ();
        
        // Dictionary with the filename as the key and a list of SaveObjects as the value
        Dictionary<string, List<ISaveable>> m_saveableObjects = new ();
        
        bool m_isSaving;
        bool m_isLoading;
        bool m_isDeleting;
        
        bool IsBusy => m_isSaving || m_isLoading || m_isDeleting;
        
        // TODOS:
        // [X] Turn this into a package
        // [ ] Add the ability to use JSONUtility instead of EasySave
        // [X] Check if File.WriteAllTextAsync works versus FileStream / StreamWriter
        // [X] Test file encryption on writes
        // [ ] Test file encryption on reads
        // [X] Test file deletes
        // [X] Test file erases
        // [ ] On Awake, get all of the Saveables register them rather than having to do it manually
        // [ ] Add save versions and data migrations
        // [ ] Create a debug visual that can be used for testing on devices
        // [ ] Test on PlayStation, Xbox, Switch, iOS, Android

        [System.Serializable]
        public struct SaveObject
        {
            public string guid;
            public string content;
        }
        
        void Awake()
        {
            m_fileHandler = new FileHandler();
        }
        
        string EncryptDecrypt(string content)
        {
            string newContent = "";
            for (int i = 0; i < content.Length; i++)
                newContent += (char)(content[i] ^ m_encryptionPassword[i % m_encryptionPassword.Length]);

            return newContent;
        }

        /// <summary>
        /// Register an ISaveable with the DataManager.
        /// </summary>
        public static void RegisterSaveable(ISaveable saveable)
        {
            // Store the saveable
            m_saveables.Add(saveable);

            // Store the filename
            m_files.Add(saveable.FileName);
        }

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

                // If we are already doing file I/O, return
                if (IsBusy)
                    return;

                m_isSaving = true;
                
                while (m_saveQueue.Count > 0)
                {
                    string[] filenamesToSave = m_saveQueue.Dequeue();
                    
                    // Clear the save objects dictionary
                    m_saveObjects.Clear();

                    // Organize all of the ISaveables by filename into a Dictionary
                    // with the filename as the key and a list of SaveObjects as the value
                    foreach (string filename in filenamesToSave)
                        foreach (ISaveable s in m_saveables)
                            if (s.FileName == filename)
                            {
                                // Add the filename key to the dictionary and create its list if it doesn't exist
                                if (!m_saveObjects.ContainsKey(filename))
                                    m_saveObjects.Add(filename, new List<SaveObject>());
                                
                                // Add the ISaveable to the filename's list of SaveObjects
                                // where the SaveObject contains the ISaveable's GUID and its JSON content
                                m_saveObjects[filename].Add(new SaveObject
                                {
                                    guid = s.Guid.ToString(),
                                    content = JsonUtility.ToJson(s)
                                });
                            }
                        
                    // Create a JSON string of each file's contents
                    foreach (KeyValuePair<string, List<SaveObject>> pair in m_saveObjects)
                    {
                        // Create a JSON string of the saveables
                        string json = JsonHelper.ToJson(pair.Value.ToArray(), true);
                        
                        // Save the JSON string to disk
                        await m_fileHandler.WriteFile(pair.Key, m_useEncryption ? EncryptDecrypt(json) : json);
                    }
                    
                    /*m_saveableObjects.Clear();
                    
                    // Organize all of the ISaveables by filename into a Dictionary
                    // with the filename as the key and a list of ISaveables as the value
                    foreach (string filename in filenamesToSave)
                    foreach (ISaveable s in m_saveables)
                        if (s.FileName == filename)
                        {
                            // Add the filename key to the dictionary and create its list if it doesn't exist
                            if (!m_saveableObjects.ContainsKey(filename))
                                m_saveableObjects.Add(filename, new List<ISaveable>());
                                
                            m_saveableObjects[filename].Add(s);
                        }
                        
                    // Create a JSON string of each file's contents
                    foreach (KeyValuePair<string, List<ISaveable>> pair in m_saveableObjects)
                    {
                        // Create a JSON string of the saveables
                        string json = JsonHelper.ToJson(pair.Value.ToArray(), true);
                        
                        // Save the JSON string to disk
                        await m_fileHandler.WriteFile(pair.Key, m_useEncryption ? EncryptDecrypt(json) : json);
                    }*/
                }

                m_isSaving = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }

        /*public async Awaitable LoadAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                // If these files are not in the queue, add them
                if (!m_loadQueue.Contains(filenames))
                    m_loadQueue.Enqueue(filenames);

                // If we are already doing file I/O, return
                if (IsBusy)
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

        /// <summary>
        /// Deletes the files at the given paths or filenames. Each file will be removed from disk.
        /// Use <see cref="EraseAsync(string[])"/> to fill each file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to delete.</param>
        /// <param name="eraseAndKeepFile">If true, files will only be erased. If false, files will be removed from disk.</param>
        public async Awaitable DeleteAsync(string[] filenames, bool eraseAndKeepFile = false)
        {
            // If the cancellation token has been requested at any point, return
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                // Switch to a background thread
                await Awaitable.BackgroundThreadAsync();
                
                // If these files are not in the queue, add them
                if (!m_deleteQueue.Contains(filenames))
                    m_deleteQueue.Enqueue(filenames);

                // If we are already doing file I/O, return
                if (IsBusy)
                    return;

                m_isDeleting = true;
                
                while (m_deleteQueue.Count > 0)
                {
                    string[] filenamesToDelete = m_deleteQueue.Dequeue();
                    
                    // Delete the files from disk
                    foreach (string filename in filenamesToDelete)
                        if (eraseAndKeepFile)
                            m_fileHandler.Erase(filename);
                        else
                            m_fileHandler.Delete(filename);
                }

                m_isDeleting = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }
        
        /// <summary>
        /// Erases the files at the given paths or filenames. Each file will still exist on disk, but it will be empty.
        /// Use <see cref="DeleteAsync(string[], bool)"/> to remove the file from disk.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to erase.</param>
        public async Awaitable EraseAsync(string[] filenames)
            => await DeleteAsync(filenames, true);
    }
}
