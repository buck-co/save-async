// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            public int SlotIndex;
            public AwaitableCompletionSource Completion;

            public FileOperation(FileOperationType operationType, string[] filenames, int slotIndex, AwaitableCompletionSource completion)
            {
                Type = operationType;
                Filenames = filenames;
                SlotIndex = slotIndex;
                Completion = completion;
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
            int Version { get; }
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
            public int Version => m_inner.Version;

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
            public int EntryVersion;
            public JToken Data;
        }

        static FileHandler m_fileHandler;

        static readonly Dictionary<string, IBoxedSaveable> m_saveables = new();
        static readonly List<LoadedSaveable> m_loadedSaveables = new();
        static readonly Queue<FileOperation> m_fileOperationQueue = new();
        static readonly Dictionary<string, StorageScope> s_fileScopes = new();

        static readonly object s_QueueLock = new();
        static bool m_initialized;

        static int s_MainThreadId;

        static readonly JsonSerializerSettings s_jsonNoTypes = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto
        };

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

        static int s_saveSlotIndex = -1;

        // While the queue is executing a request, this carries the slot index that was current when
        // that request was enqueued. AsyncLocal so it is visible only to code running inside that
        // operation's own async flow (e.g. FileHandlers resolving paths), never to game code running
        // concurrently on other flows.
        static readonly AsyncLocal<int?> s_slotIndexOverride = new();

        /// <summary>
        /// Stores the current save slot index, which can be used to determine which save slot to use for saving and loading files.
        /// A value of -1 indicates that no save slot is being used, which can be useful for settings files or other data that does not require a save slot.
        /// File operations capture this value at the time they are requested and execute with it,
        /// so changing the slot while an operation is still queued does not redirect that operation.
        /// </summary>
        public static int SaveSlotIndex
        {
            get => s_slotIndexOverride.Value ?? s_saveSlotIndex;
            set => s_saveSlotIndex = value;
        }

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
            
            var scope = saveable.Scope;
            if (s_fileScopes.TryGetValue(boxed.Filename, out var existing) && existing != scope)
                Debug.LogError($"[Save Async] Conflicting scopes for filename \"{boxed.Filename}\": {existing} vs {scope}.");
            else
                s_fileScopes[boxed.Filename] = scope;
        }
        
        public static StorageScope ResolveScopeFor(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return StorageScope.Slot; // safest default

            return s_fileScopes.GetValueOrDefault(filename, StorageScope.Slot); // default Slot unless explicitly registered as Global
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

        static CancellationTokenSource s_linkedLifetimeCts;
        static SaveManager s_linkedLifetimeOwner;

        static OperationContext CreateContext()
        {
            // Instance can be null (e.g. in the editor after exiting play mode, once the singleton
            // has been destroyed). Fall back to application-lifetime cancellation and defaults
            // instead of throwing.
            var instance = Instance;

            CancellationToken token;
            if (instance == null)
            {
                token = Application.exitCancellationToken;
            }
            else
            {
                // Cache one linked source per SaveManager instance instead of creating (and never
                // disposing) a new linked CancellationTokenSource for every call, which slowly
                // accumulates registrations on the application-lifetime token.
                if (s_linkedLifetimeOwner != instance || s_linkedLifetimeCts == null)
                {
                    s_linkedLifetimeCts?.Dispose();
                    s_linkedLifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(instance.destroyCancellationToken, Application.exitCancellationToken);
                    s_linkedLifetimeOwner = instance;
                }
                token = s_linkedLifetimeCts.Token;
            }

            return new OperationContext
            {
                UseBackgroundThread = instance != null && instance.m_useBackgroundThread,
                EncryptionType = instance != null ? instance.m_encryptionType : EncryptionType.None,
                EncryptionPassword = instance != null ? instance.m_encryptionPassword : string.Empty,
                CancellationToken = token
            };
        }

        static async Awaitable DoFileOperation(FileOperationType requestedType, string[] requestedFilenames, OperationContext ctx)
        {
            if (m_saveables.Count == 0)
            {
                Debug.LogError("[Save Async] SaveManager.DoFileOperation() - No saveables have been registered. " +
                         "Register ISaveable<TState> before using save, load, erase, or delete methods.");
                return;
            }

            // Every request gets a completion source so that awaiting a public API call always
            // means "this request has been executed" (including the restore pass for loads) - even
            // when another operation was already in progress and this request was only queued.
            var completion = new AwaitableCompletionSource();
            bool ownsBusy = false;

            lock (s_QueueLock)
            {
                // Capture the game-facing slot value (the backing field, not the property, which
                // could observe another operation's in-flight override).
                m_fileOperationQueue.Enqueue(new FileOperation(requestedType, requestedFilenames, s_saveSlotIndex, completion));

                if (!IsBusy)
                {
                    IsBusy = true;
                    ownsBusy = true;
                }
            }

            if (!ownsBusy)
            {
                // Another operation owns the queue and will execute this request. Await actual
                // completion; if the drain fails, the exception is observed here as well.
                await completion.Awaitable;
                return;
            }

            await DrainQueueAsync(ctx);
        }

        /// <summary>
        /// Runs as the single operation that owns <see cref="IsBusy"/>: repeatedly drains the queue,
        /// runs the restore pass for each batch, and completes each request. IsBusy is released
        /// atomically with the check that the queue is empty, so a request enqueued at any point is
        /// either executed by this drain or finds IsBusy false and starts its own drain. Requests
        /// are completed on the main thread, after their batch's restore pass has run.
        /// </summary>
        static async Awaitable DrainQueueAsync(OperationContext ctx)
        {
            var batch = new List<FileOperation>();

            try
            {
                while (true)
                {
                    if (ctx.UseBackgroundThread)
                        await Awaitable.BackgroundThreadAsync();

                    bool processedLoad = false;
                    bool processedLoadDefaults = false;
                    var affectedFilenames = new HashSet<string>();
                    batch.Clear();

                    while (true)
                    {
                        FileOperation fileOperation;

                        lock (s_QueueLock)
                        {
                            if (m_fileOperationQueue.Count == 0)
                                break;

                            fileOperation = m_fileOperationQueue.Dequeue();
                        }

                        batch.Add(fileOperation);

                        var result = await ExecuteOperationAsync(fileOperation, ctx, affectedFilenames);
                        processedLoad |= result.processedLoad;
                        processedLoadDefaults |= result.processedLoadDefaults;
                    }

                    // Always hop back to the main thread before touching Unity objects
                    // and before completing requests so caller continuations resume on main.
                    await Awaitable.MainThreadAsync();

                    if (processedLoad || processedLoadDefaults)
                        RestorePass(affectedFilenames, processedLoad, processedLoadDefaults);

                    m_loadedSaveables.Clear();

                    // A request is only complete once its batch's restore pass has run.
                    foreach (var op in batch)
                        op.Completion?.TrySetResult();
                    batch.Clear();

                    // Release IsBusy only when the queue is verifiably empty, atomically with the
                    // check. Requests enqueued during the restore pass or the completions above are
                    // picked up by the next iteration; requests enqueued after the release below
                    // find IsBusy false and start their own drain. Nothing can be stranded.
                    lock (s_QueueLock)
                    {
                        if (m_fileOperationQueue.Count == 0)
                        {
                            IsBusy = false;
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save Async] SaveManager.DoFileOperation() - Exception: {e.Message}\n{e.StackTrace}");

                // Fail every request this drain can no longer serve - the in-flight batch and
                // everything still queued - so their awaiters observe the exception instead of
                // waiting forever, then release the queue so later requests start fresh.
                lock (s_QueueLock)
                {
                    while (m_fileOperationQueue.Count > 0)
                        batch.Add(m_fileOperationQueue.Dequeue());

                    IsBusy = false;
                }

                foreach (var op in batch)
                    op.Completion?.TrySetException(e);

                throw;
            }
        }

        /// <summary>
        /// Executes one dequeued operation with <see cref="s_slotIndexOverride"/> set to the slot
        /// index captured when the request was made. Deliberately an async Task rather than an
        /// Awaitable: the TPL method builder isolates the caller's ExecutionContext, so the
        /// AsyncLocal override set here cannot leak into the flow that started the drain (Unity's
        /// Awaitable builder does not provide that isolation for code before the first await).
        /// </summary>
        static async Task<(bool processedLoad, bool processedLoadDefaults)> ExecuteOperationAsync(FileOperation fileOperation, OperationContext ctx, HashSet<string> affectedFilenames)
        {
            // Execute with the slot index that was current when this request was made,
            // not whatever the slot index happens to be by the time it is dequeued.
            s_slotIndexOverride.Value = fileOperation.SlotIndex;
            try
            {
                switch (fileOperation.Type)
                {
                    case FileOperationType.Save:
                        await SaveFileOperationAsync(fileOperation.Filenames, ctx);
                        return (false, false);

                    case FileOperationType.Load:
                        await LoadFileOperationAsync(fileOperation.Filenames, ctx);
                        foreach (var f in fileOperation.Filenames)
                            affectedFilenames.Add(f);
                        return (true, false);

                    case FileOperationType.Delete:
                        await DeleteFileOperationAsync(fileOperation.Filenames, eraseAndKeepFile: false, ctx);
                        return (false, false);

                    case FileOperationType.Erase:
                        await DeleteFileOperationAsync(fileOperation.Filenames, eraseAndKeepFile: true, ctx);
                        return (false, false);

                    case FileOperationType.LoadDefaults:
                        foreach (var f in fileOperation.Filenames)
                            affectedFilenames.Add(f);
                        return (false, true);

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            finally
            {
                s_slotIndexOverride.Value = null;
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

                    // Version check: if the on-disk entry's Version doesn't match the registered saveable's Version,
                    // skip old data and explicitly restore defaults for this saveable.
                    if (loaded.EntryVersion != boxed.Version)
                    {
                        Debug.LogWarning($"[Save Async] SaveManager.DoFileOperation() - Version mismatch for key \"{loaded.Key}\". " +
                                         $"Save data has Version {loaded.EntryVersion}; runtime expects {boxed.Version}. Defaults will be used.");
                        boxed.RestoreStateBoxed(null);
                        restoredSaveables[loaded.Key] = true;
                        continue;
                    }

                    try
                    {
                        object state = loaded.Data?.ToObject(boxed.StateType, JsonSerializer.CreateDefault(s_jsonNoTypes));
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
                            int entryVersion = item["Version"]?.Value<int?>() ?? 0; // legacy entries will be 0
                            var data = item["Data"];
                            m_loadedSaveables.Add(new LoadedSaveable
                            {
                                Key = key,
                                EntryVersion = entryVersion,
                                Data = data
                            });
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

                var token = JToken.FromObject(data, JsonSerializer.CreateDefault(s_jsonNoTypes));

                var obj = new JObject
                {
                    ["Key"] = s.Key,
                    ["Version"] = s.Version,
                    ["Data"] = token
                };

                array.Add(obj);
            }

            return array.ToString(Formatting.Indented);
        }
    }
}
