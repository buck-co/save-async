using UnityEngine;
using System.Threading;
using System.IO;

namespace Buck.DataManagement
{
    public static class FileHandler
    {
        static string PersistentDataPath;
        
        static CancellationTokenSource m_CancellationTokenSource;
        static CancellationToken DestroyCancellationToken
        {
            get
            {
                m_CancellationTokenSource ??= new CancellationTokenSource();
                return m_CancellationTokenSource.Token;
            }
        }
        
        /// <summary>
        /// This is called from the DataManager's Awake() method.
        /// It stores the persistent data path so that save methods can be called from any thread.
        /// </summary>
        public static void Initialize()
            => PersistentDataPath = Application.persistentDataPath;
        
        static string GetPath(string filename)
            => Path.Combine(PersistentDataPath, filename);

        public static bool Exists(string filename)
            => File.Exists(GetPath(filename));
        
        public static async Awaitable WriteFile(string filename, string content)
        {
            // If the cancellation token has been requested at any point, return
            while (!DestroyCancellationToken.IsCancellationRequested)
            {
                // Switch to a background thread for writing the file
                await Awaitable.BackgroundThreadAsync();

                FileStream fileStream = new FileStream(GetPath(filename), FileMode.Create);

                await using StreamWriter writer = new StreamWriter(fileStream);
                await writer.WriteAsync(content);
                
                return;
            }
        }
        
        public static async Awaitable<string> ReadFile(string filename)
        {
            // If the cancellation token has been requested at any point, return
            while (!DestroyCancellationToken.IsCancellationRequested)
            {
                // Switch to a background thread for reading the file
                await Awaitable.BackgroundThreadAsync();

                FileStream fileStream = new FileStream(GetPath(filename), FileMode.Open);

                using StreamReader reader = new StreamReader(fileStream);
                string content = await reader.ReadToEndAsync();

                return content;
            }

            return null;
        }

        public static void Delete(string filename) 
            => File.Delete(GetPath(filename));
    }
}
