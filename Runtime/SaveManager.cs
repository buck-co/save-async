// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace Buck.SaveAsync
{
    [AddComponentMenu("SaveAsync/SaveManager")]
    public class SaveManager : Singleton<SaveManager>
    {
        [SerializeField, Tooltip("Generally you should keep this enabled and only disable it if you believe " + 
                                 "that it's causing unexpected behavior on a target platform.")]
        bool m_useBackgroundThread = true;
        
        [SerializeField, Tooltip("Enables encryption for save data. " +
                                 "XOR encryption is basic but extremely fast. Support for AES encryption is planned." +
                                 "Do not change the encryption type once the game has shipped!")]
        EncryptionType m_encryptionType = EncryptionType.None;
        
        [SerializeField, Tooltip("The password used to encrypt and decrypt save data. This password should be unique to your game. " +
                                 "Do not change the encryption password once the game has shipped!")]
        string m_encryptionPassword = "password";
        
        [SerializeField, Tooltip("This field can be left blank. SaveAsync allows the FileHandler class to be overridden." +
                                 "This can be useful in scenarios where files should not be saved using local file IO" +
                                 "(such as cloud saves) or when a platform-specific save API must be used. " +
                                 "If you want to use a custom file handler, create a new class that inherits from FileHandler and assign it here.")]
        FileHandler m_customFileHandler;

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
        static Dictionary<string, ISaveable> m_saveables = new();
        static List<SaveableObject> m_loadedSaveables = new();
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
        public class SaveableObject
        {
            public string Key;
            public object Data;
        }

        void Awake()
            => Initialize();

        static void Initialize()
        {
            if (m_isInitialized)
                return;

            // If there is a user-defined FileHandler, use it. Otherwise, create a new FileHandler.
            m_fileHandler = Instance.m_customFileHandler == null
                          ? ScriptableObject.CreateInstance<FileHandler>()
                          : Instance.m_customFileHandler;
            
            m_isInitialized = true;
        }

        #region SaveAsync API

        /// <summary>
        /// Boolean indicating whether or not a file operation is in progress.
        /// </summary>
        public static bool IsBusy { get; private set; }

        /// <summary>
        /// Registers an ISaveable and its file for saving and loading.
        /// </summary>
        /// <param name="saveable">The ISaveable to register for saving and loading.</param>
        public static void RegisterSaveable(ISaveable saveable)
        {
            if (m_saveables.TryAdd(saveable.Key, saveable))
                m_files.Add(saveable.Filename);
            else
                Debug.LogError($"Saveable with Key {saveable.Key} already exists!");
        }
        
        /// <summary>
        /// Checks if a file exists at the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to check for existence.</param>
        public static async Task<bool> Exists(string filename)
            => await m_fileHandler.Exists(filename);

        /// <summary>
        /// Saves the files at the given paths or filenames.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to save.</param>
        public static async Awaitable Save(string[] filenames)
            => await DoFileOperation(FileOperationType.Save, filenames);
        
        /// <summary>
        /// Saves the file at the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to save.</param>
        public static async Awaitable Save(string filename)
            => await Save(new[] {filename});
        
        /// <summary>
        /// Loads the files at the given paths or filenames.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to load.</param>
        public static async Awaitable Load(string[] filenames)
            => await DoFileOperation(FileOperationType.Load, filenames);
        
        /// <summary>
        /// Loads the file at the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to load.</param>
        public static async Awaitable Load(string filename)
            => await Load(new[] {filename});

        /// <summary>
        /// Deletes the files at the given paths or filenames. Each file will be removed from disk.
        /// Use <see cref="Erase(string[])"/> to fill each file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to delete.</param>
        public static async Awaitable Delete(string[] filenames)
            => await DoFileOperation(FileOperationType.Delete, filenames);
        
        /// <summary>
        /// Deletes the file at the given path or filename. The file will be removed from disk.
        /// Use <see cref="Erase(string)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to delete.</param>
        public static async Awaitable Delete(string filename)
            => await Delete(new[] {filename});
        
        /// <summary>
        /// Erases the files at the given paths or filenames. Each file will still exist on disk, but it will be empty.
        /// Use <see cref="Delete(string[])"/> to remove the files from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to erase.</param>
        public static async Awaitable Erase(string[] filenames)
            => await DoFileOperation(FileOperationType.Erase, filenames);
        
        /// <summary>
        /// Erases the file at the given path or filename. The file will still exist on disk, but it will be empty.
        /// Use <see cref="Delete(string)"/> to remove the file from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to erase.</param>
        public static async Awaitable Erase(string filename)
            => await Erase(new[] {filename});
        
        /// <summary>
        /// Sets the given Guid byte array to a new Guid byte array if it is null, empty, or an empty Guid.
        /// This method can be useful for creating unique keys for ISaveables.
        /// </summary>
        /// <param name="guidBytes">The byte array (passed by reference) that you would like to fill with a serializable guid.</param>
        /// <returns>The same byte array that contains the serializable guid, but returned from the method.</returns>
        public static byte[] GetSerializableGuid(ref byte[] guidBytes)
        {
            // If the byte array is null, return a new Guid byte array.
            if (guidBytes == null)
            {
                Debug.LogWarning("Guid byte array is null. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            // If the byte array is empty, return a new Guid byte array.
            if (guidBytes.Length == 0)
            {
                Debug.LogWarning("Guid byte array is empty. Generating a new Guid.");
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
                Debug.LogWarning("Guid is empty. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            return guidBytes;
        }
        
        #endregion
        
        static async Awaitable DoFileOperation(FileOperationType operationType, string[] filenames)
        {
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
                if (Instance.m_useBackgroundThread)
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
                if (Instance.m_useBackgroundThread)
                    await Awaitable.MainThreadAsync();
                
                // If anything was populated in the loadedDataList, restore state
                // This is done here because it's better to process the whole queue before switching back to the main thread.
                if (m_loadedSaveables.Count > 0)
                {
                    // Restore state for each ISaveable
                    foreach (SaveableObject wrappedData in m_loadedSaveables)
                    {
                        if (wrappedData.Key == null)
                        {
                            Debug.LogError("The key for an ISaveable is null. JSON data may be malformed. " +
                                           "The data will not be restored. ", Instance.gameObject);
                            continue;
                        }
                        
                        // Try to get the ISaveable from the dictionary
                        if (m_saveables.ContainsKey(wrappedData.Key) == false)
                        {
                            Debug.LogError("The ISaveable with the key " + wrappedData.Key + " was not found in the saveables dictionary. " +
                                           "The data will not be restored. This could mean that the string Key for the matching object has " +
                                           "changed since the save data was created.", Instance.gameObject);
                            continue;
                        }
                        
                        // Get the ISaveable from the dictionary
                        var saveable = m_saveables[wrappedData.Key];

                        // If the ISaveable is null, log an error and continue to the next iteration
                        if (saveable == null)
                        {
                            Debug.LogError("The ISaveable with the key " + wrappedData.Key + " is null. "
                                           + "The data will not be restored.", Instance.gameObject);
                            continue;
                        }
                        
                        // Restore the state of the ISaveable
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
                    if (saveable.Filename == filename)
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
                List<SaveableObject> jsonObjects = null;
                
                try
                {
                    jsonObjects = JsonConvert.DeserializeObject<List<SaveableObject>>(json, m_jsonSerializerSettings);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error deserializing JSON data. JSON data may be malformed. Exception message: " + e.Message, Instance.gameObject);
                    continue;
                }
                
                if (jsonObjects != null)
                    m_loadedSaveables.AddRange(jsonObjects);
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

            SaveableObject[] wrappedSaveables = new SaveableObject[saveables.Count];

            for (var i = 0; i < saveables.Count; i++)
            {
                var s = saveables[i];
                var data = s.CaptureState();

                wrappedSaveables[i] = new SaveableObject
                {
                    Key = s.Key.ToString(),
                    Data = data
                };
            }

            return JsonConvert.SerializeObject(wrappedSaveables, m_jsonSerializerSettings);
        }
    }
}
