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
        static List<ISaveable> m_saveables = new();
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
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        
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
        // [ ] Test paths and folders
        // [ ] Test FileHandler.Exists()
        // [ ] Add XML comments to all public methods
        // [/] Make better encryption (AES is WIP and encryption works, but something is wrong with decryption)
        // [ ] Add more error handling (i.e. if a file isn't registered that's being saved to, etc.)
        // [ ] On Awake, get all of the Saveables register them rather than having to do it manually?
        // [ ] Add save versions and data migrations
        // [ ] Create a debug visual that can be used for testing on devices
        // [ ] Add data adapters for platforms where necessary (could be inherited from FileHandler)
        // [ ] Test on other platforms, i.e. PlayStation, Xbox, Switch, iOS, Android
        // [ ] Add support for save backups
        // [ ] Write tests
        
        void Awake()
            => m_fileHandler = new FileHandler();

        /// <summary>
        /// Registers an ISaveable and its file with the DataManager.
        /// </summary>
        public static void RegisterSaveable(ISaveable saveable)
        {
            m_saveables.Add(saveable);
            m_files.Add(saveable.FileName);
        }

        public static async Awaitable SaveAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
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

                // Process the save queue until it's empty
                while (m_saveQueue.Count > 0)
                {
                    // Get the next set of files to save
                    string[] filenamesToSave = m_saveQueue.Dequeue();

                    // Get the saveables that correspond to the files, convert them to JSON, and save them
                    foreach (string filename in filenamesToSave)
                    {
                        string json = await ToJson(m_saveables.FindAll(s => s.FileName == filename));
                        await m_fileHandler.WriteFile(filename, Encrpytion.Encrypt(json, Instance.m_encryptionPassword, Instance.m_encryptionType));
                    }
                }

                m_isSaving = false;
                
                // Return, otherwise we will loop forever
                return;
            }
        }
        
        static async Awaitable<string> ToJson(List<ISaveable> saveables)
        {
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
            {
                // Switch to a background thread
                await Awaitable.BackgroundThreadAsync();

                if (saveables == null)
                    throw new System.ArgumentNullException(nameof(saveables));

                SaveableData[] wrappedSaveables = new SaveableData[saveables.Count];

                for (var i = 0; i < saveables.Count; i++)
                {
                    var s = saveables[i];
                    var data = s.CaptureState();

                    SaveableData wrappedData = new()
                    {
                        Guid = s.Guid.ToString(),
                        TypeName = data.GetType().AssemblyQualifiedName,
                        Data = data
                    };

                    wrappedSaveables[i] = wrappedData;
                }

                return JsonConvert.SerializeObject(wrappedSaveables, m_jsonSerializerSettings);
            }

            return null;
        }
        
        public static async Awaitable LoadAsync(string[] filenames)
        {
            // If the cancellation token has been requested at any point, return
            while (!Instance.destroyCancellationToken.IsCancellationRequested)
            {
                // Switch to a background thread
                await Awaitable.BackgroundThreadAsync();
                
                // If these files are not in the queue, add them
                if (!m_loadQueue.Contains(filenames))
                    m_loadQueue.Enqueue(filenames);

                // If we are already doing file I/O, return
                if (IsBusy)
                    return;

                m_isLoading = true;

                // Process the load queue until it's empty
                while (m_loadQueue.Count > 0)
                {
                    // Get the next set of files to load
                    string[] filenamesToLoad = m_loadQueue.Dequeue();

                    // Load the files
                    foreach (string filename in filenamesToLoad)
                    {
                        string fileContent = await m_fileHandler.ReadFile(filename);
                        string json = Encrpytion.Decrypt(fileContent, Instance.m_encryptionPassword, Instance.m_encryptionType);
                        
                        // Deserialize the JSON data to List of SaveableDataWrapper
                        List<SaveableData> loadedDataList = JsonConvert.DeserializeObject<List<SaveableData>>(json, m_jsonSerializerSettings);

                        // Switch back to the main thread before accessing Unity objects
                        await Awaitable.MainThreadAsync();
                        
                        // Restore state for each saveable
                        foreach (SaveableData wrappedData in loadedDataList)
                        {
                            var guid = new System.Guid(wrappedData.Guid);
                            
                            var saveable = m_saveables.Find(s => s.Guid == guid);

                            if (saveable != null)
                            {
                                // Determine the specific type from the TypeName
                                System.Type theDataType = System.Type.GetType(wrappedData.TypeName);
                                if (theDataType != null)
                                {
                                    try
                                    {
                                        // Deserialize the data into the correct subtype of SaveableData
                                        SaveableData dataInstance = (SaveableData)JsonConvert.DeserializeObject(wrappedData.Data.ToString(), theDataType, m_jsonSerializerSettings);

                                        // Restore state with the deserialized data
                                        saveable.RestoreState(dataInstance);
                                    }
                                    catch (System.Exception e)
                                    {
                                        Debug.LogError($"Failed to restore state for {saveable.FileName} with GUID {saveable.Guid} and type {theDataType}.\n{e}");
                                    }
                                }
                            }
                        }
                    }
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
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="filenames">The array of paths or filenames to erase.</param>
        public static async Awaitable EraseAsync(string[] filenames)
            => await DeleteAsync(filenames, true);
    }
}
