// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
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

        [SerializeField, Tooltip("Enables device save conflicts before each r/w operation. " +
                                 "Ex: conflict is raised if current device is Android and last save was on IOS.")] 
        private bool _validateDeviceMatchOnEachSave;
        [SerializeField, Tooltip("Enabling this will skip device match validation.")]
        private bool _ignoreDeviceMatching;
        
        
        

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

        public static string DeviceSaveDataFile => m_deviceSaveData.Filename;

        /// <summary>
        /// Action raised when discrepancy in device data.
        /// </summary>
        public static Action<DeviceSaveDataConflict> DeviceConflictFoundEvent = conflict => { };
        /// <summary>
        /// Action raised when discrepancy in device data.
        /// </summary>
        public static Action ConflictOverwriteOccurredEvent = () => { };
        

        static DeviceSaveData m_deviceSaveData;
        static FileHandler m_fileHandler;
        static Dictionary<string, ISaveable> m_saveables = new();
        static List<SaveableObject> m_loadedSaveables = new();
        static Queue<FileOperation> m_fileOperationQueue = new();
        static readonly HashSet<string> m_files = new();
        static HashSet<string> m_filesToResave = new();
        
        static bool m_isInitialized;
        static bool m_conflictLock;
        
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
            
            m_deviceSaveData = new DeviceSaveData();
            RegisterSaveable(m_deviceSaveData);
            
            m_isInitialized = true;
            
        }

        class TimeStamp : ISaveable
        {
            public static readonly string TimestampPrefix = "Timestamp_{0}";
            public string Key => string.Format(TimestampPrefix, _filename);
            public string Filename => _filename;
            private string _filename;
            public void SetFile(string filename)
            {
                _filename = filename;
            }
            
            [Serializable]
            public struct SaveData
            {
                public string timestamp;
            }
            
            public object CaptureState()
            {
                return new SaveData()
                {
                    timestamp = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)
                };
            }

            public void RestoreState(object state)
            {
                
            }
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
            {
                // Attempt to add a timestamp savable to each file registered
                var timestampObject = new TimeStamp();
                timestampObject.SetFile(saveable.Filename);
                if (m_saveables.TryAdd(timestampObject.Key, timestampObject))
                {
                    Debug.LogWarning($"timecheck. Successfully Added {timestampObject.Key}");
                }
                m_files.Add(saveable.Filename);
            }
                
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
        {
            var existsResult = await m_fileHandler.Exists(filename, Instance.destroyCancellationToken);
            return existsResult.Local || existsResult.Remote;
        }

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
                Debug.LogWarning("SaveManager.cs: Guid byte array is null. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            // If the byte array is empty, return a new Guid byte array.
            if (guidBytes.Length == 0)
            {
                Debug.LogWarning("SaveManager.cs: Guid byte array is empty. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            // If the byte array is not empty, but is not 16 bytes long, throw an exception.
            if (guidBytes.Length != 16)
                throw new ArgumentException("SaveManager.cs: Guid byte array must be 16 bytes long.");

            // If the byte array is not an empty Guid, return a new Guid byte array.
            // Otherwise, return the given Guid byte array.
            Guid guidObj = new Guid(guidBytes);

            if (guidObj == Guid.Empty)
            {
                Debug.LogWarning("SaveManager.cs: Guid is empty. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }
            
            return guidBytes;
        }
        
        #endregion

        public static async Awaitable ResolveConflict(bool keepLocal)
        {
            if (keepLocal)
            {
                await DoFileOperation(FileOperationType.Save, m_files.ToArray(), true);
            }
            else
            {
                await DoFileOperation(FileOperationType.Load, m_files.Where(f => f != m_deviceSaveData.Filename).ToArray(), true);
                await DoFileOperation(FileOperationType.Save, new []{m_deviceSaveData.Filename}, true);
            }
            m_conflictLock = false;
        }
        
        static async Awaitable DoFileOperation(FileOperationType operationType, string[] filenames, bool force = false)
        {
            Initialize();
            
            // Don't accept file operations while conflict requires resolving.
            if (m_conflictLock && !force) return;
            
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
            {
                if (m_saveables.Count == 0)
                {
                    Debug.LogError("SaveManager.cs: No saveables have been registered! You must call RegisterSaveable on your" +
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
                    
                    // run device match validation according to settings
                    if (!force && Instance._validateDeviceMatchOnEachSave 
                        && !Instance._ignoreDeviceMatching 
                        && fileOperation.Type is FileOperationType.Save or FileOperationType.Load)
                    {
                        if (await RunDeviceConflictCheck(false))
                        {
                            m_fileOperationQueue.Clear();
                            continue;
                        }
                    }
                    
                    switch (fileOperation.Type)
                    {
                        case FileOperationType.Save:
                            // Always write device save data on every operation.
                            await SaveFileOperationAsync(fileOperation.Filenames.Append(m_deviceSaveData.Filename).ToArray());
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
                            Debug.LogError("SaveManager.cs: The key for an ISaveable is null. JSON data may be malformed. " +
                                           "The data will not be restored. ", Instance.gameObject);
                            continue;
                        }
                        
                        // Try to get the ISaveable from the dictionary
                        if (m_saveables.ContainsKey(wrappedData.Key) == false)
                        {
                            Debug.LogError("SaveManager.cs: The ISaveable with the key " + wrappedData.Key + " was not found in the saveables dictionary. " +
                                           "The data will not be restored. This could mean that the string Key for the matching object has " +
                                           "changed since the save data was created.", Instance.gameObject);
                            continue;
                        }
                        
                        // Get the ISaveable from the dictionary
                        var saveable = m_saveables[wrappedData.Key];

                        // If the ISaveable is null, log an error and continue to the next iteration
                        if (saveable == null)
                        {
                            Debug.LogError("SaveManager.cs: The ISaveable with the key " + wrappedData.Key + " is null. "
                                           + "The data will not be restored.", Instance.gameObject);
                            continue;
                        }
                        
                        // Restore the state of the ISaveable
                        saveable.RestoreState(wrappedData.Data);
                    }
                }
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
                    if (saveable.Filename == filename){
                        saveablesToSave.Add(saveable);
                    }
                        
                        
                string json = SaveablesToJson(saveablesToSave);

                json = Encryption.Encrypt(json, Instance.m_encryptionPassword, Instance.m_encryptionType);
                await m_fileHandler.WriteFile(filename, json, Instance.destroyCancellationToken);
            }
        }

        static async Awaitable LoadFileOperationAsync(string[] filenames)
        {
            List<string> filesToResave = new();
            
            // Helper function to safely deserialize JSON and log errors
            List<SaveableObject> DeserializeJson(string json, string context)
            {
                try
                {
                    return JsonConvert.DeserializeObject<List<SaveableObject>>(json, m_jsonSerializerSettings);
                }
                catch (Exception e)
                {
                    Debug.LogError($"SaveManager.cs: Error deserializing {context} JSON. Exception: {e.Message}", Instance.gameObject);
                    return null;
                }
            }
            
            // Load the files
            foreach (string filename in filenames)
            {
                var loadResult = await m_fileHandler.ReadFile(filename, Instance.destroyCancellationToken);
                
                // If the file is empty, skip it
                if (string.IsNullOrEmpty(loadResult.Local) && string.IsNullOrEmpty(loadResult.Remote))
                {
                    Debug.Log($"SaveManager.cs: {filename} Both results were null or empty");
                    continue;
                }
                    
                string localJson = Encryption.Decrypt(loadResult.Local, Instance.m_encryptionPassword, Instance.m_encryptionType);
                string remoteJson = Encryption.Decrypt(loadResult.Remote, Instance.m_encryptionPassword, Instance.m_encryptionType);
                
                // Deserialize the JSON data to List of SaveableDataWrapper
                
                var localjsonObjects = DeserializeJson(localJson, "local");
                var remotejsonObjects = DeserializeJson(remoteJson, "remote");
                
                Debug.Log($"SaveManager.cs: {filename} local:{!string.IsNullOrEmpty(localJson)}  remote: {!string.IsNullOrEmpty(remoteJson)} networkError: {loadResult.NetworkError}\nlocal content: {localJson} \nremote content: {remoteJson}");
                
                // Load remote data if it is the only existing data.
                if (string.IsNullOrEmpty(localJson) && !string.IsNullOrEmpty(remoteJson))
                {
                    Debug.Log($"SaveManager.cs: {filename} Load remote data, write data to local file");
                    m_loadedSaveables.AddRange(remotejsonObjects);
                    continue;
                }

                // Load local data if it is the only existing data
                if (!string.IsNullOrEmpty(localJson) && string.IsNullOrEmpty(remoteJson))
                {
                    Debug.Log($"SaveManager.cs: {filename} load local");
                    m_loadedSaveables.AddRange(localjsonObjects);
                    if (!loadResult.NetworkError)
                    {
                        Debug.Log($"SaveManager.cs: {filename} Write local to remote");
                        filesToResave.Add(filename);
                    }
                    continue;
                }

                if (!(string.IsNullOrEmpty(localJson) && string.IsNullOrEmpty(remoteJson)))
                {
                    Debug.Log($"SaveManager.cs: {filename} both local and remote have data");
                    var localHasTimestamp = localjsonObjects.Exists(obj =>
                        obj.Key == string.Format(TimeStamp.TimestampPrefix, filename));
                    var remoteHasTimestamp = remotejsonObjects.Exists(obj =>
                        obj.Key == string.Format(TimeStamp.TimestampPrefix, filename));
                    Debug.Log($"SaveManager.cs: {filename} local timestamp exists {localHasTimestamp} remote timestamp exists {localHasTimestamp} ");
                    
                    if (localHasTimestamp != remoteHasTimestamp)
                    {
                        Debug.Log($"SaveManager.cs: {filename} Loading local? {localHasTimestamp}");
                        m_loadedSaveables.AddRange(localHasTimestamp ? localjsonObjects : remotejsonObjects);
                        if (!(loadResult.NetworkError && localHasTimestamp))
                        {
                            Debug.Log($"SaveManager.cs: {filename} Write local to remote because timestamps are missing");
                            filesToResave.Add(filename);
                        }
                        continue;
                    }
                    if (!(localHasTimestamp && remoteHasTimestamp))
                    {
                        Debug.Log($"SaveManager.cs: {filename} Neither has timestamp, load objects and write file again to add timestamps");
                        m_loadedSaveables.AddRange(remotejsonObjects);
                        filesToResave.Add(filename);
                        continue;
                    }
                    var localTimestamp = localjsonObjects.Find(obj => obj.Key == string.Format(TimeStamp.TimestampPrefix, filename));
                    var remoteTimestamp = remotejsonObjects.Find(obj => obj.Key == string.Format(TimeStamp.TimestampPrefix, filename));

                    
                    if (((TimeStamp.SaveData)localTimestamp.Data).timestamp !=
                                                    ((TimeStamp.SaveData)remoteTimestamp.Data).timestamp)
                    {
                        var localTime = DateTime.Parse(((TimeStamp.SaveData)localTimestamp.Data).timestamp);
                        var remoteTime = DateTime.Parse(((TimeStamp.SaveData)remoteTimestamp.Data).timestamp);
                        Debug.Log($"SaveManager.cs: {filename} remote not null, {localTime > remoteTime} local:{localTime} remote: {remoteTime}");
                        m_loadedSaveables.AddRange(localTime > remoteTime ? localjsonObjects : remotejsonObjects);
                        Debug.Log($"SaveManager.cs: {filename} Resaving file");
                        filesToResave.Add(filename);
                    }
                    else
                    {
                        Debug.Log($"SaveManager.cs: {filename} Timestamps match, loading remote data");
                        m_loadedSaveables.AddRange(remotejsonObjects);
                    }
                }
            }
            m_fileOperationQueue.Enqueue(new FileOperation(FileOperationType.Save, filesToResave.ToArray()));
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns>True, if conflict was found</returns>
        public static async Task<bool> RunDeviceConflictCheck(bool awaitResolution)
        {
            var result = await CheckDeviceInfo(Instance.destroyCancellationToken);
            if (result.Conflict != null)
            {
                m_conflictLock = true;
                if (!Instance.m_useBackgroundThread)
                {
                    await Awaitable.MainThreadAsync();
                }

                DeviceConflictFoundEvent?.Invoke((DeviceSaveDataConflict)result.Conflict);
                while (awaitResolution && m_conflictLock)
                {
                    if (Instance.m_useBackgroundThread)
                    {
                        await Awaitable.WaitForSecondsAsync(0.1f, Instance.destroyCancellationToken);
                    }
                    else
                    {
                        await Awaitable.NextFrameAsync(Instance.destroyCancellationToken);
                    }
                }
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Ensures Device save data exists and returns conflict if it exists
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        static async Task<DeviceSaveDataResult> CheckDeviceInfo(CancellationToken cancellationToken)
        {

            var exists = await m_fileHandler.Exists(m_deviceSaveData.Filename, cancellationToken);
            if (!(exists.Local || exists.Remote))
            {
                Debug.Log($"SaveManager.cs: Device Info exists for (Local: {exists.Local}) (remote: {exists.Remote}) rewriting");
                await m_fileHandler.WriteFile(m_deviceSaveData.Filename, SaveablesToJson(new List<ISaveable>(){m_deviceSaveData}), cancellationToken);
            }
            exists = await m_fileHandler.Exists(m_deviceSaveData.Filename, cancellationToken);
            var fileContent = await m_fileHandler.ReadFile(m_deviceSaveData.Filename, cancellationToken);
            Debug.Log($"SaveManager.cs: Remote Device Info Save Data {fileContent.Remote}");
            if (!exists.Remote)
            {
                fileContent.Remote = fileContent.Local;
            }
            try
            {
                var json = Encryption.Decrypt(fileContent.Remote, Instance.m_encryptionPassword, Instance.m_encryptionType);
                Debug.Log($"SaveManager.cs: Remote Device Info:\n{json}");
                var remoteInfo = JsonConvert.DeserializeObject<List<SaveableObject>>(json, m_jsonSerializerSettings);
                
                return remoteInfo != null && m_deviceSaveData.Equals(remoteInfo.Find(o => o.Key == m_deviceSaveData.Key).Data) 
                    ? new DeviceSaveDataResult((DeviceSaveData.SaveData)remoteInfo.Find(o => o.Key == m_deviceSaveData.Key).Data) 
                    : new DeviceSaveDataResult(new DeviceSaveDataConflict(m_deviceSaveData.LastSavedState, (DeviceSaveData.SaveData)remoteInfo[0].Data));
            }
            catch (Exception e)
            {
                Debug.LogError("SaveManager.cs: Error deserializing JSON data. JSON data may be malformed. Exception message: " + e.Message, Instance.gameObject);
                throw;
            }
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
        
        public struct DeviceSaveDataResult
        {
            public readonly DeviceSaveData.SaveData? SaveData;
            public readonly DeviceSaveDataConflict? Conflict;

            public DeviceSaveDataResult(DeviceSaveData.SaveData data)
            {
                SaveData = data;
                Conflict = null;
            }

            public DeviceSaveDataResult(DeviceSaveDataConflict conflict)
            {
                SaveData = null;
                Conflict = conflict;
            }
        }
    
        public struct DeviceSaveDataConflict
        {
            public readonly DeviceSaveData.SaveData Local;
            public readonly DeviceSaveData.SaveData Remote;

            public DeviceSaveDataConflict(DeviceSaveData.SaveData local, DeviceSaveData.SaveData remote)
            {
                Local = local;
                Remote = remote;
            }
        }
    }
}
