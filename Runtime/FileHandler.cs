// MIT License - Copyright (c) 2023 BUCK Design LLC - https://github.com/buck-co

using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Buck.DataManagement
{
    public class FileHandler
    {
        string m_persistentDataPath;
        
        /// <summary>
        /// Creates a new FileHandler instance that stores Application.persistentDataPath, which can only be accessed on the main thread.
        /// Also creates a cancellation token for async methods.
        /// </summary>
        public FileHandler()
            => m_persistentDataPath = Application.persistentDataPath;

        /// <summary>
        /// Returns the full path to a file in the persistent data path using the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file that will be combined with the persistent data path.</param>
        string GetPath(string pathOrFilename)
            => Path.Combine(m_persistentDataPath, pathOrFilename);

        /// <summary>
        /// Returns true if a file exists at the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
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
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to write.</param>
        /// <param name="content">The string to write to the file.</param>
        /// <param name="useBackgroundThread">True by default. Set to false to run on the main thread.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public async Task WriteFile(string pathOrFilename, string content, CancellationToken cancellationToken)
            => await File.WriteAllTextAsync(GetPath(pathOrFilename), content, cancellationToken);

        /// <summary>
        /// Returns the contents of a file at the given path or filename.
        /// This is an asynchronous method. If useBackgroundThread is true, it runs on a background thread, 
        /// otherwise it runs on the main thread.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to read.</param>
        /// <param name="useBackgroundThread">True by default. Set to false to run on the main thread.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public async Task<string> ReadFile(string pathOrFilename, CancellationToken cancellationToken)
        {
            // If the file does not exist, return an empty string and log a warning.
            if (!Exists(pathOrFilename))
            {
                Debug.LogWarning($"FileHandler: File does not exist at path or filename: {pathOrFilename}" +
                                 $"\nReturning empty string and no data will be loaded.");
                return string.Empty;
            }
                
            string fileContent = await File.ReadAllTextAsync(GetPath(pathOrFilename), cancellationToken);
            
            // If the file is empty, return an empty string and log a warning.
            if (string.IsNullOrEmpty(fileContent))
            {
                Debug.LogWarning($"FileHandler: Attempted to load {pathOrFilename} but the file was empty.");
                return string.Empty;
            }

            return fileContent;
        }
        
        /// <summary>
        /// Erases a file at the given path or filename. The file will still exist on disk, but it will be empty.
        /// Use <see cref="Delete(string)"/> to remove the file from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to erase.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public async Task Erase(string pathOrFilename, CancellationToken cancellationToken)
            => await WriteFile(pathOrFilename, string.Empty, cancellationToken);

        /// <summary>
        /// Deletes a file at the given path or filename. This will remove the file from disk.
        /// Use <see cref="Erase(string, CancellationToken)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to delete.</param>
        public void Delete(string pathOrFilename) 
            => File.Delete(GetPath(pathOrFilename));
    }
}
