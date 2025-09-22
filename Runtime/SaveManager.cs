// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Buck.SaveAsync
{
    [AddComponentMenu("SaveAsync/SaveManager")]
    public class SaveManager : Singleton<SaveManager>
    {
        [SerializeField, Tooltip("Background threads are still an experimental feature and are turned off by default. " +
                                 "They do increase performance in many instances, but exceptions on a background thread " +
                                 "may not be caught and logged in Unity, and methods might fail silently. Use with caution!")]
        bool m_useBackgroundThread = false;

        [SerializeField, Tooltip("Enables encryption for save data. " +
                                 "XOR encryption is basic but extremely fast. Support for AES encryption is planned." +
                                 "Do not change the encryption type once the game has shipped!")]
        EncryptionType m_encryptionType = EncryptionType.None;

        [SerializeField, Tooltip(
             "The password used to encrypt and decrypt save data. This password should be unique to your game. " +
             "Do not change the encryption password once the game has shipped!")]
        string m_encryptionPassword = "password";

        [SerializeField, Tooltip(
             "This field can be left blank. SaveAsync allows the FileHandler class to be overridden." +
             "This can be useful in scenarios where files should not be saved using local file IO" +
             "(such as cloud saves) or when a platform-specific save API must be used. " +
             "If you want to use a custom file handler, create a new class that inherits from FileHandler and assign it here.")]
        FileHandler m_customFileHandler;

        enum FileOperationType
        {
            Save,
            Load,
            Delete,
            Erase,
            LoadDefaults
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

        struct OperationContext
        {
            public bool UseBackgroundThread;
            public EncryptionType EncryptionType;
            public string EncryptionPassword;
            public CancellationToken CancellationToken;
        }

        interface IBoxedSaveable
        {
            string Key { get; }
            string Filename { get; }
            Type StateType { get; }
            object CaptureStateBoxed();
            void RestoreStateBoxed(object state);
        }

        sealed class BoxedSaveable<TState> : IBoxedSaveable
        {
            readonly ISaveable<TState> m_inner;

            public BoxedSaveable(ISaveable<TState> inner) => m_inner = inner;

            public string Key => m_inner.Key;
            public string Filename => m_inner.Filename;
            public Type StateType => typeof(TState);

            public object CaptureStateBoxed() => m_inner.CaptureState();

            public void RestoreStateBoxed(object state)
            {
                if (state is null)
                {
                    m_inner.RestoreState(default);
                    return;
                }

                m_inner.RestoreState((TState)state);
            }
        }

        sealed class LoadedSaveable
        {
            public string Key;
            public JToken Data;
        }

        static FileHandler m_fileHandler;

        static readonly Dictionary<string, IBoxedSaveable> m_saveables = new();
        static readonly List<LoadedSaveable> m_loadedSaveables = new();
        static readonly Queue<FileOperation> m_fileOperationQueue = new();

        static readonly object s_QueueLock = new();
        static bool m_initialized;

        static int s_MainThreadId;

        static readonly JsonSerializerSettings s_jsonNoTypes = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None
        };

        static readonly JsonSerializer s_serializerNoTypes = JsonSerializer.Create(s_jsonNoTypes);

        static bool IsMainThread => Environment.CurrentManagedThreadId == s_MainThreadId;

        void Awake()
        {
            s_MainThreadId = Environment.CurrentManagedThreadId;
            Initialize();
        }

        static void Initialize()
        {
            if (m_initialized)
                return;

            m_fileHandler = Instance != null && Instance.m_customFileHandler != null
                ? Instance.m_customFileHandler
                : ScriptableObject.CreateInstance<FileHandler>();

            m_initialized = true;
        }

        #region SaveAsync API

        /// <summary>
        /// Boolean indicating whether a file operation is in progress.
        /// </summary>
        public static bool IsBusy { get; private set; }

        /// <summary>
        /// Stores the current save slot index, which can be used to determine which save slot to use for saving and loading files.
        /// A value of -1 indicates that no save slot is being used, which can be useful for settings files or other data that does not require a save slot.
        /// </summary>
        public static int SaveSlotIndex { get; set; } = -1;

        /// <summary>
        /// Registers an ISaveable and its file for saving and loading.
        /// </summary>
        /// <typeparam name="TState">The serializable state type for this saveable.</typeparam>
        /// <param name="saveable">The ISaveable to register for saving and loading.</param>
        public static void RegisterSaveable<TState>(ISaveable<TState> saveable)
        {
            Initialize();

            if (saveable == null)
            {
                Debug.LogWarning("[Save Async] SaveManager.RegisterSaveable() - Attempted to register a null ISaveable.");
                return;
            }

            var boxed = new BoxedSaveable<TState>(saveable);
            if (!m_saveables.TryAdd(boxed.Key, boxed))
                Debug.LogWarning($"[Save Async] SaveManager.RegisterSaveable() - Saveable with Key \"{boxed.Key}\" already exists.");
        }

        /// <summary>
        /// Unregisters a previously registered ISaveable by key. This is useful when unloading scenes.
        /// </summary>
        /// <param name="key">The unique key of the ISaveable to unregister.</param>
        public static void UnregisterSaveable(string key)
        {
            Initialize();

            if (string.IsNullOrEmpty(key))
                return;

            m_saveables.Remove(key);
        }

        /// <summary>
        /// Checks if a file exists at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to check for existence.</param>
        public static bool Exists(string filename)
        {
            Initialize();
            return m_fileHandler.Exists(filename);
        }

        /// <summary>
        /// Saves the files at the given paths or filenames.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to save.</param>
        public static async Awaitable Save(string[] filenames)
        {
            Initialize();
            var ctx = CreateContext();
            if (ctx.CancellationToken.IsCancellationRequested)
                return;

            await DoFileOperation(FileOperationType.Save, filenames, ctx);
        }

        /// <summary>
        /// Saves the file at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to save.</param>
        public static async Awaitable Save(string filename)
            => await Save(new[] { filename });

        /// <summary>
        /// Loads the files at the given paths or filenames.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to load.</param>
        public static async Awaitable Load(string[] filenames)
        {
            Initialize();
            var ctx = CreateContext();
            if (ctx.CancellationToken.IsCancellationRequested)
                return;

            await DoFileOperation(FileOperationType.Load, filenames, ctx);
        }

        /// <summary>
        /// Loads the file at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to load.</param>
        public static async Awaitable Load(string filename)
            => await Load(new[] { filename });

        /// <summary>
        /// Triggers loading without file I/O. Any saved files will be ignored and RestoreState() will be passed a null value.
        /// This can be useful if you want RestoreState() to use default values, such as when working in the Unity Editor
        /// where you may want to test default states without loading save data.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames whose ISaveables should be reset to defaults.</param>
        public static async Awaitable LoadDefaults(string[] filenames)
        {
            Initialize();
            var ctx = CreateContext();
            if (ctx.CancellationToken.IsCancellationRequested)
                return;

            await DoFileOperation(FileOperationType.LoadDefaults, filenames, ctx);
        }

        /// <summary>
        /// Triggers loading without file I/O. Any saved files will be ignored and RestoreState() will be passed a null value.
        /// This can be useful if you want RestoreState() to use default values, such as when working in the Unity Editor
        /// where you may want to test default states without loading save data.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename whose ISaveables should be reset to defaults.</param>
        public static async Awaitable LoadDefaults(string filename)
            => await LoadDefaults(new[] { filename });

        /// <summary>
        /// Deletes the files at the given paths or filenames. Each file will be removed from disk.
        /// Use <see cref="Erase(string[])"/> to fill each file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to delete.</param>
        public static async Awaitable Delete(string[] filenames)
        {
            Initialize();
            var ctx = CreateContext();
            if (ctx.CancellationToken.IsCancellationRequested)
                return;

            await DoFileOperation(FileOperationType.Delete, filenames, ctx);
        }

        /// <summary>
        /// Deletes the file at the given path or filename. The file will be removed from disk.
        /// Use <see cref="Erase(string)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to delete.</param>
        public static async Awaitable Delete(string filename)
            => await Delete(new[] { filename });

        /// <summary>
        /// Erases the files at the given paths or filenames. Each file will still exist on disk, but it will be empty.
        /// Use <see cref="Delete(string[])"/> to remove the files from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to erase.</param>
        public static async Awaitable Erase(string[] filenames)
        {
            Initialize();
            var ctx = CreateContext();
            if (ctx.CancellationToken.IsCancellationRequested)
                return;

            await DoFileOperation(FileOperationType.Erase, filenames, ctx);
        }

        /// <summary>
        /// Erases the file at the given path or filename. The file will still exist on disk, but it will be empty.
        /// Use <see cref="Delete(string)"/> to remove the file from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="filename">The path or filename to erase.</param>
        public static async Awaitable Erase(string filename)
            => await Erase(new[] { filename });

        /// <summary>
        /// Sets the given Guid byte array to a new Guid byte array if it is null, empty, or an empty Guid.
        /// This method can be useful for creating unique keys for ISaveables.
        /// </summary>
        /// <param name="guidBytes">The byte array (passed by reference) that you would like to fill with a serializable guid.</param>
        /// <returns>The same byte array that contains the serializable guid, but returned from the method.</returns>
        public static byte[] GetSerializableGuid(ref byte[] guidBytes)
        {
            if (guidBytes == null)
            {
                Debug.LogWarning("[Save Async] SaveManager.GetSerializableGuid() - Guid byte array is null. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }

            if (guidBytes.Length == 0)
            {
                Debug.LogWarning("[Save Async] SaveManager.GetSerializableGuid() - Guid byte array is empty. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }

            if (guidBytes.Length != 16)
                throw new ArgumentException("[Save Async] SaveManager.GetSerializableGuid() - Guid byte array must be 16 bytes long.");

            Guid guidObj = new Guid(guidBytes);

            if (guidObj == Guid.Empty)
            {
                Debug.LogWarning("[Save Async] SaveManager.GetSerializableGuid() - Guid is empty. Generating a new Guid.");
                guidBytes = Guid.NewGuid().ToByteArray();
            }

            return guidBytes;
        }

        #endregion

        static OperationContext CreateContext()
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(Instance.destroyCancellationToken, Application.exitCancellationToken).Token;
            return new OperationContext
            {
                UseBackgroundThread = Instance && Instance.m_useBackgroundThread,
                EncryptionType = Instance ? Instance.m_encryptionType : EncryptionType.None,
                EncryptionPassword = Instance ? Instance.m_encryptionPassword : string.Empty,
                CancellationToken = linked
            };
        }

        static async Awaitable DoFileOperation(FileOperationType requestedType, string[] requestedFilenames, OperationContext ctx)
        {
            try
            {
                if (m_saveables.Count == 0)
                {
                    Debug.LogError("[Save Async] SaveManager.DoFileOperation() - No saveables have been registered. " +
                             "Register ISaveable<TState> before using save, load, erase, or delete methods.");
                    return;
                }

                lock (s_QueueLock)
                {
                    m_fileOperationQueue.Enqueue(new FileOperation(requestedType, requestedFilenames));
                    if (IsBusy)
                        return;

                    IsBusy = true;
                }

                if (ctx.UseBackgroundThread)
                    await Awaitable.BackgroundThreadAsync();

                bool processedLoad = false;
                bool processedLoadDefaults = false;
                var affectedFilenames = new HashSet<string>();

                while (true)
                {
                    FileOperation fileOperation;

                    lock (s_QueueLock)
                    {
                        if (m_fileOperationQueue.Count == 0)
                            break;

                        fileOperation = m_fileOperationQueue.Dequeue();
                    }

                    switch (fileOperation.Type)
                    {
                        case FileOperationType.Save:
                            await SaveFileOperationAsync(fileOperation.Filenames, ctx);
                            break;

                        case FileOperationType.Load:
                            await LoadFileOperationAsync(fileOperation.Filenames, ctx);
                            processedLoad = true;
                            foreach (var f in fileOperation.Filenames)
                                affectedFilenames.Add(f);
                            break;

                        case FileOperationType.Delete:
                            await DeleteFileOperationAsync(fileOperation.Filenames, eraseAndKeepFile: false, ctx);
                            break;

                        case FileOperationType.Erase:
                            await DeleteFileOperationAsync(fileOperation.Filenames, eraseAndKeepFile: true, ctx);
                            break;

                        case FileOperationType.LoadDefaults:
                            processedLoadDefaults = true;
                            foreach (var f in fileOperation.Filenames)
                                affectedFilenames.Add(f);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (ctx.UseBackgroundThread)
                    await Awaitable.MainThreadAsync();

                if (processedLoad || processedLoadDefaults)
                    RestorePass(affectedFilenames, processedLoad, processedLoadDefaults);

                m_loadedSaveables.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save Async] SaveManager.DoFileOperation() - Exception: {e.Message}\n{e.StackTrace}");
                throw;
            }
            finally
            {
                lock (s_QueueLock)
                {
                    IsBusy = false;
                }
            }
        }

        static void RestorePass(HashSet<string> affectedFilenames, bool didLoad, bool didDefaults)
        {
            var restoredSaveables = new Dictionary<string, bool>(m_saveables.Count);
            foreach (var kvp in m_saveables)
                restoredSaveables[kvp.Key] = false;

            if (didLoad && m_loadedSaveables.Count > 0)
            {
                foreach (var loaded in m_loadedSaveables)
                {
                    if (loaded.Key == null)
                    {
                        Debug.LogError("[Save Async] SaveManager.DoFileOperation() - The key for an ISaveable was null. JSON data may be malformed.");
                        continue;
                    }

                    if (!m_saveables.TryGetValue(loaded.Key, out var boxed) || boxed == null)
                    {
                        Debug.LogError($"[Save Async] SaveManager.DoFileOperation() - The ISaveable with the key \"{loaded.Key}\" was not found or is null. The data will not be restored.");
                        continue;
                    }

                    try
                    {
                        object state = loaded.Data?.ToObject(boxed.StateType, s_serializerNoTypes);
                        boxed.RestoreStateBoxed(state);
                        restoredSaveables[loaded.Key] = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Save Async] SaveManager.DoFileOperation() - Failed to restore state for key \"{loaded.Key}\": {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }

            foreach (var kvp in m_saveables)
            {
                if (restoredSaveables[kvp.Key])
                    continue;

                if (!affectedFilenames.Contains(kvp.Value.Filename))
                    continue;

                kvp.Value.RestoreStateBoxed(null);

                if (didLoad)
                {
                    Debug.LogWarning($"[Save Async] SaveManager.DoFileOperation() - The ISaveable with the key \"{kvp.Key}\" " +
                               "was not restored from save data. This could mean the save data did not contain any data for this ISaveable.");
                }
            }

            if (didDefaults)
                Debug.Log("[Save Async] SaveManager.DoFileOperation() - Saveables were loaded with default state because LoadDefaults() was called.");
        }

        static async Awaitable SaveFileOperationAsync(string[] filenames, OperationContext ctx)
        {
            var ct = ctx.CancellationToken;
            if (ct.IsCancellationRequested)
                return;

            try
            {
                foreach (string filename in filenames)
                {
                    var toSave = new List<IBoxedSaveable>();
                    foreach (var s in m_saveables.Values)
                        if (s.Filename == filename)
                            toSave.Add(s);

                    string json = SaveablesToJson(toSave);
                    if (string.IsNullOrEmpty(json))
                        throw new InvalidOperationException($"[Save Async] SaveManager.SaveFileOperationAsync() - JSON serialization returned empty for file \"{filename}\".");

                    string encrypted = Encryption.Encrypt(json, ctx.EncryptionPassword, ctx.EncryptionType);
                    await m_fileHandler.WriteFile(filename, encrypted, ct).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save Async] SaveManager.SaveFileOperationAsync() - Exception: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        static async Awaitable LoadFileOperationAsync(string[] filenames, OperationContext ctx)
        {
            var ct = ctx.CancellationToken;
            if (ct.IsCancellationRequested)
                return;

            try
            {
                foreach (string filename in filenames)
                {
                    string fileContent = await m_fileHandler.ReadFile(filename, ct).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(fileContent))
                        continue;

                    string json = Encryption.Decrypt(fileContent, ctx.EncryptionPassword, ctx.EncryptionType);

                    try
                    {
                        var array = JArray.Parse(json);
                        foreach (var item in array)
                        {
                            var key = item["Key"]?.ToString();
                            var data = item["Data"];
                            m_loadedSaveables.Add(new LoadedSaveable { Key = key, Data = data });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Save Async] SaveManager.LoadFileOperationAsync() - Error deserializing JSON data: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save Async] SaveManager.LoadFileOperationAsync() - Exception: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        static async Awaitable DeleteFileOperationAsync(string[] filenames, bool eraseAndKeepFile, OperationContext ctx)
        {
            var ct = ctx.CancellationToken;
            if (ct.IsCancellationRequested)
                return;

            try
            {
                foreach (string filename in filenames)
                {
                    if (eraseAndKeepFile)
                        await m_fileHandler.Erase(filename, ct).ConfigureAwait(false);
                    else
                        await m_fileHandler.Delete(filename, ct).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save Async] SaveManager.DeleteFileOperationAsync() - Exception: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        static string SaveablesToJson(List<IBoxedSaveable> saveables)
        {
            if (saveables == null)
                throw new ArgumentNullException(nameof(saveables));

            var array = new JArray();

            foreach (var s in saveables)
            {
                object data = null;
                try
                {
                    data = s.CaptureStateBoxed();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Save Async] SaveManager.SaveablesToJson() - Failed to capture state for ISaveable with key \"{s.Key}\": {e.Message}\n{e.StackTrace}");
                }

                var token = JToken.FromObject(data, s_serializerNoTypes);

                var obj = new JObject
                {
                    ["Key"] = s.Key,
                    ["Data"] = token
                };

                array.Add(obj);
            }

            return array.ToString(Formatting.Indented);
        }
    }
}
