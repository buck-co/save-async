// TODOS:
// [X] Turn this into a package
// [X] Add the ability to use JSON instead of EasySave
// [X] Check if File.WriteAllTextAsync works versus FileStream / StreamWriter
// [X] Test basic XOR file encryption on writes
// [X] Test basic XOR file encryption on reads
// [X] Test file deletes
// [X] Test file erases
// [X] Add support for serializable collections (done via Json.NET)
// [X] Make methods static
// [X] Improve performance by replacing JObject in SaveableData with a string
// [X] Add JsonConverters for Vector3 and other common Unity types (done via Newtonsoft.Json.UnityConverters)
// [X] Figure out how to support custom Unity types within each class's generic ISaveable object type (maybe use inheritance?)
// [X] Figure out why Guid, TypeName, and Data are being added to the serialized JSON string
// [ ] Figure out a solve for git dependencies on Newtonsoft.Json.UnityConverters and BUCK Basics (UPM doesn't support git dependencies)
// [X] Figure out why spamming output (especially on queue tests) shows 0ms on console output
// [ ] Figure out why the first load test doesn't work
// [ ] Check for empty files before loading
// [X] Improve performance of loading by batching all of the wrapped saveables before switching back to the main thread
// [ ] Need to test for file existence before attempting to load. Does the FileHandler already do this?
// [ ] Test paths and folders
// [ ] Test FileHandler.Exists()
// [ ] Add XML comments to all public methods
// [/] Make better encryption (AES is WIP and encryption works, but something is wrong with decryption)
// [ ] Should the collections be concurrent types?
// [ ] Add more error handling (i.e. if a file isn't registered that's being saved to, etc.)
// [ ] On Awake, get all of the Saveables register them rather than having to do it manually?
// [ ] Add save versions and data migrations
// [ ] Create a debug visual that can be used for testing on devices
// [ ] Add data adapters for platforms where necessary (could be inherited from FileHandler)
// [ ] Test on other platforms, i.e. PlayStation, Xbox, Switch, iOS, Android
// [ ] Add support for save backups
// [ ] Write tests
        
using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Buck.DataManagement
{
    public class DataManager : Singleton<DataManager>
    {
        [SerializeField, Tooltip("Enables encryption for save data." +
                                 "Note, AES is the most secure encryption type but also takes significantly longer than XOR or no encryption." +
                                 "Do not change the encryption type once the game has shipped!")]
        EncryptionType m_encryptionType = EncryptionType.None;
        
        [SerializeField, Tooltip("The password used to encrypt and decrypt save data. This password should be unique to your game." +
                                 "Do not change the encryption password once the game has shipped!")]
        string m_encryptionPassword = "password";
        
        static FileHandler m_fileHandler;
        static Dictionary<Guid, ISaveable> m_saveables = new();
        static HashSet<string> m_files = new();
        static Queue<string[]> m_saveQueue = new();
        static Queue<string[]> m_loadQueue = new();
        static Queue<string[]> m_deleteQueue = new();
        
        static bool m_isSaving;
        static bool m_isLoading;
        static bool m_isDeleting;
        
        static bool IsBusy => m_isSaving || m_isLoading || m_isDeleting;

        static readonly JsonSerializerSettings m_jsonSerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto
        };
        
        public static int SaveQueueCount => m_saveQueue.Count;
        public static int LoadQueueCount => m_loadQueue.Count;
        public static int DeleteQueueCount => m_deleteQueue.Count;

        [Serializable]
        public class SaveableDataWrapper
        {
            public string Guid;
            public object Data;
        }
        
        void Awake()
            => m_fileHandler = new FileHandler();

        /// <summary>
        /// Registers an ISaveable and its file with the DataManager for saving and loading.
        /// </summary>
        /// <param name="saveable">The ISaveable to register with the data manager for saving and loading.</param>
        public static void RegisterSaveable(ISaveable saveable)
        {
            if (m_saveables.TryAdd(saveable.Guid, saveable))
                m_files.Add(saveable.FileName);
            else
                Debug.LogError($"Saveable with GUID {saveable.Guid} already exists!");
        }

        /// <summary>
        /// Saves the files at the given paths or filenames.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to save.</param>
        public static async Awaitable SaveAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
            {
                // If these files are not in the queue, add them
                if (!m_saveQueue.Contains(filenames))
                    m_saveQueue.Enqueue(filenames);
                
                // If we are already doing file I/O, wait
                if (IsBusy)
                    await Awaitable.NextFrameAsync();
                
                // Semaphore to prevent multiple saves from happening at once
                m_isSaving = true;
                
                // Switch to a background thread
                if (m_saveQueue.Count > 0)
                    await Awaitable.BackgroundThreadAsync();

                // Process the save queue until it's empty
                while (m_saveQueue.Count > 0)
                {
                    // Get the next set of files to save
                    string[] filenamesToSave = m_saveQueue.Dequeue();

                    // Get the saveables that correspond to the files, convert them to JSON, and save them
                    foreach (string filename in filenamesToSave)
                    {
                        List<ISaveable> saveablesToSave = new();
                        
                        // Gather all of the saveables that correspond to the file
                        foreach (ISaveable saveable in m_saveables.Values)
                            if (saveable.FileName == filename)
                                saveablesToSave.Add(saveable);
                        
                        string json = SaveablesToJson(saveablesToSave);
                        json = Encrpytion.Encrypt(json, Instance.m_encryptionPassword, Instance.m_encryptionType);
                        await m_fileHandler.WriteFile(filename, json, Instance.destroyCancellationToken);
                    }
                }
                
                m_isSaving = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }
        
        static string SaveablesToJson(List<ISaveable> saveables)
        {
            if (saveables == null)
                throw new ArgumentNullException(nameof(saveables));

            SaveableDataWrapper[] wrappedSaveables = new SaveableDataWrapper[saveables.Count];

            for (var i = 0; i < saveables.Count; i++)
            {
                var s = saveables[i];
                var data = s.CaptureState();

                wrappedSaveables[i] = new SaveableDataWrapper
                {
                    Guid = s.Guid.ToString(),
                    Data = data
                };
            }

            return JsonConvert.SerializeObject(wrappedSaveables, m_jsonSerializerSettings);
        }
        
        /// <summary>
        /// Loads the files at the given paths or filenames.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to load.</param>
        public static async Awaitable LoadAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
            {
                // If these files are not in the queue, add them
                if (!m_loadQueue.Contains(filenames))
                    m_loadQueue.Enqueue(filenames);

                // If we are already doing file I/O, wait
                if (IsBusy)
                    await Awaitable.NextFrameAsync();

                // Semaphore to prevent multiple loads from happening at once
                m_isLoading = true;
                
                // Switch to a background thread
                if (m_loadQueue.Count > 0)
                    await Awaitable.BackgroundThreadAsync();
                
                // Create the list of SaveableDataWrappers to restore state from
                List<SaveableDataWrapper> loadedDataList = new();
                
                // Process the load queue until it's empty
                while (m_loadQueue.Count > 0)
                {
                    // Get the next set of files to load
                    string[] filenamesToLoad = m_loadQueue.Dequeue();

                    // Load the files
                    foreach (string filename in filenamesToLoad)
                    {
                        string fileContent = await m_fileHandler.ReadFile(filename, Instance.destroyCancellationToken);
                        
                        // If the file is empty, skip it
                        if (string.IsNullOrEmpty(fileContent))
                        {
                            Debug.LogWarning($"Attempted to load {filename} but the file was empty.");
                            continue;
                        }
                        
                        string json = Encrpytion.Decrypt(fileContent, Instance.m_encryptionPassword, Instance.m_encryptionType);
                        
                        // Deserialize the JSON data to List of SaveableDataWrapper
                        loadedDataList.AddRange(JsonConvert.DeserializeObject<List<SaveableDataWrapper>>(json, m_jsonSerializerSettings));
                    }
                }
                
                // Switch back to the main thread before accessing Unity objects
                await Awaitable.MainThreadAsync();
                
                // Restore state for each saveable
                foreach (SaveableDataWrapper wrappedData in loadedDataList)
                {
                    var guid = new Guid(wrappedData.Guid);

                    var saveable = m_saveables[guid];

                    if (saveable != null)
                        saveable.RestoreState(wrappedData.Data);
                }
                
                m_isLoading = false;

                // Return, otherwise we will loop forever
                return;
            }
        }

        /// <summary>
        /// Deletes the files at the given paths or filenames. Each file will be removed from disk.
        /// Use <see cref="EraseAsync(string[])"/> to fill each file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to delete.</param>
        /// <param name="eraseAndKeepFile">If true, files will only be erased. If false, files will be removed from disk.</param>
        public static async Awaitable DeleteAsync(string[] filenames, bool eraseAndKeepFile = false)
        {
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
            {
                // If these files are not in the queue, add them
                if (!m_deleteQueue.Contains(filenames))
                    m_deleteQueue.Enqueue(filenames);

                // If we are already doing file I/O, wait
                if (IsBusy)
                    await Awaitable.NextFrameAsync();

                // Semaphore to prevent multiple deletes from happening at once
                m_isDeleting = true;
                
                // Switch to a background thread
                if (m_deleteQueue.Count > 0)
                    await Awaitable.BackgroundThreadAsync();
                
                while (m_deleteQueue.Count > 0)
                {
                    string[] filenamesToDelete = m_deleteQueue.Dequeue();
                    
                    // Delete the files from disk
                    foreach (string filename in filenamesToDelete)
                        if (eraseAndKeepFile)
                            await m_fileHandler.Erase(filename, Instance.destroyCancellationToken);
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
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to erase.</param>
        public static async Awaitable EraseAsync(string[] filenames)
            => await DeleteAsync(filenames, true);
    }
}
