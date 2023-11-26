using UnityEngine;
using System.Threading;
using System.IO;

namespace Buck.DataManagement
{
    public class FileHandler
    {
        string m_persistentDataPath;
        
        CancellationTokenSource m_cancellationTokenSource;
        CancellationToken DestroyCancellationToken
            => m_cancellationTokenSource.Token;

        /// <summary>
        /// Creates a new FileHandler instance that stores Application.persistentDataPath, which can only be accessed on the main thread.
        /// Also creates a cancellation token for async methods.
        /// </summary>
        public FileHandler()
        {
            m_persistentDataPath = Application.persistentDataPath;
            m_cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Returns the full path to a file in the persistent data path using the given path or filename.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file that will be combined with the persistent data path.</param>
        string GetPath(string pathOrFilename)
            => Path.Combine(m_persistentDataPath, pathOrFilename);

        /// <summary>
        /// Returns true if a file exists at the given path or filename.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to check.</param>
        public bool Exists(string pathOrFilename)
            => File.Exists(GetPath(pathOrFilename));
        
        /// <summary>
        /// Writes the given content to a file at the given path or filename.
        /// This is an asynchronous method. If useBackgroundThread is true, it runs on a background thread, 
        /// otherwise it runs on the main thread.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to write.</param>
        /// <param name="content">The string to write to the file.</param>
        /// <param name="useBackgroundThread">True by default. Set to false to run on the main thread.</param>
        public async Awaitable WriteFile(string pathOrFilename, string content, bool useBackgroundThread = true)
        {
            if (useBackgroundThread)
                await Awaitable.BackgroundThreadAsync();
            
            await File.WriteAllTextAsync(GetPath(pathOrFilename), content, DestroyCancellationToken);
        }
        
        /// <summary>
        /// Returns the contents of a file at the given path or filename.
        /// This is an asynchronous method. If useBackgroundThread is true, it runs on a background thread, 
        /// otherwise it runs on the main thread.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to read.</param>
        /// <param name="useBackgroundThread">True by default. Set to false to run on the main thread.</param>
        public async Awaitable<string> ReadFile(string pathOrFilename, bool useBackgroundThread = true)
        {
            if (useBackgroundThread)
                await Awaitable.BackgroundThreadAsync();
            
            return await File.ReadAllTextAsync(GetPath(pathOrFilename), DestroyCancellationToken);
        }
        
        /// <summary>
        /// Erases a file at the given path or filename. The file will still exist on disk, but it will be empty.
        /// Use <see cref="Delete(string)"/> to remove the file from disk.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to erase.</param>
        public void Erase(string pathOrFilename) 
            => File.WriteAllText(GetPath(pathOrFilename), string.Empty);

        /// <summary>
        /// Deletes a file at the given path or filename. This will remove the file from disk.
        /// Use <see cref="Erase(string)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.json"
        /// Path example: "MyFolder/MyFile.json"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to delete.</param>
        public void Delete(string pathOrFilename) 
            => File.Delete(GetPath(pathOrFilename));
    }
}
