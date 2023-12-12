/*

TODOS:

    [ ] Add comments and documentation for the samples
    [ ] Update Github readme

0.4.0

    [ ] Test paths and folders
    [ ] Add AES encryption
    [ ] Should the collections be concurrent types?
    [ ] Add more error handling (i.e. if a file isn't registered that's being saved to, etc.)
    [ ] On Awake, get all of the Saveables register them rather than having to do it manually?

0.5.0

    [ ] Add save versions and data migrations

0.6.0

    [ ] Add data adapters for platforms where necessary (could be inherited from FileHandler)
    [ ] Test on closed platforms, i.e. PlayStation, Xbox, Switch, iOS, Android

Post 1.0 Ideas

    [ ] Create a loading bar prefab that can be used for loading screens
    [ ] Add support for multiple save slots
    [ ] Add support for multiple users? (particularly on Steam)
    [ ] Add support for save backups
    [ ] Add support for save cloud syncing (necessary for cross-platform saves beyond just Steam)
    [ ] Write tests
    
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Buck.DataManagement
{
    public class DataManager : Buck.DataManagement.Singleton<DataManager>
    {
        [SerializeField, Tooltip("Enables encryption for save data. " +
                                 "XOR encryption is basic but extremely fast. Support for AES encryption is planned." +
                                 "Do not change the encryption type once the game has shipped!")]
        EncryptionType m_encryptionType = EncryptionType.None;
        
        [SerializeField, Tooltip("The password used to encrypt and decrypt save data. This password should be unique to your game. " +
                                 "Do not change the encryption password once the game has shipped!")]
        string m_encryptionPassword = "password";

        [SerializeField, Tooltip("Enables the use of background threads for saving and loading which greatly improves performance. " +
                                 "However, only use this if you are not using any Unity objects in your save data!")]
        bool m_useBackgroundThreads = true;

        enum FileOperationType
        {
            Save,
            Load,
            Delete,
            Erase
        }
        
        struct FileOperation
        {
            public FileOperationType Type;
            public string[] Filenames;

            public FileOperation(FileOperationType operationType, string[] filenames)
            {
                Type = operationType;
                Filenames = filenames;
            }
        }
        
        static FileHandler m_fileHandler;
        static Dictionary<Guid, ISaveable> m_saveables = new();
        static List<SaveableDataWrapper> m_loadedSaveables = new();
        static Queue<FileOperation> m_fileOperationQueue = new();
        static HashSet<string> m_files = new();
        
        static bool m_isInitialized;
        
        static readonly JsonSerializerSettings m_jsonSerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto
        };

        [Serializable]
        public class SaveableDataWrapper
        {
            public string Guid;
            public object Data;
        }

        void Awake()
            => Initialize();

        static void Initialize()
        {
            if (m_isInitialized)
                return;
            
            m_fileHandler = new FileHandler();
            m_isInitialized = true;
        }

        #region DataManager API

        /// <summary>
        /// Boolean indicating whether or not the DataManager is currently busy with a file operation.
        /// </summary>
        public static bool IsBusy { get; private set; }

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
            => await DoFileOperation(FileOperationType.Save, filenames);
        
        /// <summary>
        /// Loads the files at the given paths or filenames.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to load.</param>
        public static async Awaitable LoadAsync(string[] filenames)
            => await DoFileOperation(FileOperationType.Load, filenames);

        /// <summary>
        /// Deletes the files at the given paths or filenames. Each file will be removed from disk.
        /// Use <see cref="EraseAsync(string[])"/> to fill each file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to delete.</param>
        public static async Awaitable DeleteAsync(string[] filenames)
            => await DoFileOperation(FileOperationType.Delete, filenames);
        
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
            => await DoFileOperation(FileOperationType.Erase, filenames);
        
        #endregion
        
        static async Awaitable DoFileOperation(FileOperationType operationType, string[] filenames)
        {
            // Initialize the DataManager if it hasn't been already
            Initialize();
            
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
            {
                if (m_saveables.Count == 0)
                {
                    Debug.LogError("No saveables have been registered! You must call RegisterSaveable on your" +
                                   " ISaveable classes before using save, load, erase, or delete methods.", Instance.gameObject);
                    return;
                }
                
                // Create the file operation struct and queue it
                m_fileOperationQueue.Enqueue(new FileOperation(operationType, filenames));

                // If we are already doing file I/O, return
                if (IsBusy)
                    return;

                // Prevent duplicate file operations from processing the queue
                IsBusy = true;
                
                // Switch to a background thread to process the queue
                if (Instance.m_useBackgroundThreads)
                    await Awaitable.BackgroundThreadAsync();

                while (m_fileOperationQueue.Count > 0)
                {
                    m_fileOperationQueue.TryDequeue(out FileOperation fileOperation);
                    switch (fileOperation.Type)
                    {
                        case FileOperationType.Save:
                            await SaveFileOperationAsync(fileOperation.Filenames);
                            break;
                        case FileOperationType.Load:
                            await LoadFileOperationAsync(fileOperation.Filenames);
                            break;
                        case FileOperationType.Delete:
                            await DeleteFileOperationAsync(fileOperation.Filenames);
                            break;
                        case FileOperationType.Erase:
                            await DeleteFileOperationAsync(fileOperation.Filenames, true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                // Switch back to the main thread before accessing Unity objects and setting IsBusy to false
                await Awaitable.MainThreadAsync();
                
                // If anything was populated in the loadedDataList, restore state
                // This is done here because it's better to process the whole queue before switching back to the main thread.
                if (m_loadedSaveables.Count > 0)
                {
                    // Restore state for each ISaveable
                    foreach (SaveableDataWrapper wrappedData in m_loadedSaveables)
                    {
                        var guid = new Guid(wrappedData.Guid);

                        var saveable = m_saveables[guid];

                        if (saveable != null)
                            saveable.RestoreState(wrappedData.Data);
                    }
                }
                
                // Clear the list before the next iteration
                m_loadedSaveables.Clear();

                IsBusy = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }

        static async Awaitable SaveFileOperationAsync(string[] filenames)
        {
            // Get the ISaveables that correspond to the files, convert them to JSON, and save them
            foreach (string filename in filenames)
            {
                List<ISaveable> saveablesToSave = new();
                        
                // Gather all of the saveables that correspond to the file
                foreach (ISaveable saveable in m_saveables.Values)
                    if (saveable.FileName == filename)
                        saveablesToSave.Add(saveable);
                        
                string json = SaveablesToJson(saveablesToSave);
                json = Encryption.Encrypt(json, Instance.m_encryptionPassword, Instance.m_encryptionType);
                await m_fileHandler.WriteFile(filename, json, Instance.destroyCancellationToken);
            }
        }

        static async Awaitable LoadFileOperationAsync(string[] filenames)
        {
            // Load the files
            foreach (string filename in filenames)
            {
                string fileContent = await m_fileHandler.ReadFile(filename, Instance.destroyCancellationToken);
                        
                // If the file is empty, skip it
                if (string.IsNullOrEmpty(fileContent))
                    continue;
                        
                string json = Encryption.Decrypt(fileContent, Instance.m_encryptionPassword, Instance.m_encryptionType);
                        
                // Deserialize the JSON data to List of SaveableDataWrapper
                m_loadedSaveables.AddRange(JsonConvert.DeserializeObject<List<SaveableDataWrapper>>(json, m_jsonSerializerSettings));
            }
        }
        
        static async Awaitable DeleteFileOperationAsync(string[] filenames, bool eraseAndKeepFile = false)
        {
            // Delete the files from disk
            foreach (string filename in filenames)
                if (eraseAndKeepFile)
                    await m_fileHandler.Erase(filename, Instance.destroyCancellationToken);
                else
                    m_fileHandler.Delete(filename);
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
        /// Sets the given Guid byte array to a new Guid byte array if it is null, empty, or an empty Guid.
        /// </summary>
        public static byte[] GetSerializableGuid(ref byte[] guidBytes)
        {
            // If the byte array is null, return a new Guid byte array.
            if (guidBytes == null)
            {
                Debug.Log("Guid byte array is null. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            // If the byte array is empty, return a new Guid byte array.
            if (guidBytes.Length == 0)
            {
                Debug.Log("Guid byte array is empty. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            // If the byte array is not empty, but is not 16 bytes long, throw an exception.
            if (guidBytes.Length != 16)
                throw new ArgumentException("Guid byte array must be 16 bytes long.");

            // If the byte array is not an empty Guid, return a new Guid byte array.
            // Otherwise, return the given Guid byte array.
            Guid guidObj = new Guid(guidBytes);

            if (guidObj == Guid.Empty)
            {
                Debug.Log("Guid is empty. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            return guidBytes;
        }
    }
}
