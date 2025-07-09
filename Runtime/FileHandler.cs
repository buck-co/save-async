// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Buck.SaveAsync
{
    public class FileHandler : ScriptableObject
    {
        /// <summary>
        /// Stores the persistent data path for later use, which can only be accessed on the main thread.
        /// </summary>
        protected string m_persistentDataPath;
        
        /// <summary>
        /// The suffix to append to the filename. By default, it is "_editor" when in the Unity Editor, and an empty string in builds.
        /// </summary>
        protected string FilenameSuffix
#if UNITY_EDITOR
            => "_editor";
#else
            => string.Empty;
#endif
        
        /// <summary>
        /// The file extension to use for the files. By default, it is ".dat".
        /// </summary>
        protected string FileExtension => ".dat";
        
        
        protected virtual void OnEnable()
            => m_persistentDataPath = Application.persistentDataPath;
        
        /// <summary>
        /// Returns the path to a file using the given path or filename and appends the <see cref="FilenameSuffix"/> and 
        /// <see cref="FileExtension"/> but does not include the persistent data path.
        /// For the full path, use <see cref="GetFullPath(string)"/>.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file that will be combined with the persistent data path.</param>
        protected string GetPartialPath(string pathOrFilename)
        {
            // Ensure the filename is valid and append the suffix and extension.
            if (string.IsNullOrEmpty(pathOrFilename))
                throw new System.ArgumentException("Path or filename cannot be null or empty.", nameof(pathOrFilename));
            
            return $"{pathOrFilename}{FilenameSuffix}{FileExtension}";
        }
        
        /// <summary>
        /// Returns the full path to a file in the persistent data path using the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file that will be combined with the persistent data path.</param>
        protected virtual string GetFullPath(string pathOrFilename)
            => Path.Combine(m_persistentDataPath, GetPartialPath(pathOrFilename));

        /// <summary>
        /// Returns true if a file exists at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to check.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task<bool> Exists(string pathOrFilename, CancellationToken cancellationToken)
            => File.Exists(GetFullPath(pathOrFilename));

        /// <summary>
        /// Writes the given content to a file at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to write.</param>
        /// <param name="content">The string to write to the file.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task WriteFile(string pathOrFilename, string content, CancellationToken cancellationToken)
            => await File.WriteAllTextAsync(GetFullPath(pathOrFilename), content, cancellationToken);

        /// <summary>
        /// Returns the contents of a file at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to read.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task<string> ReadFile(string pathOrFilename, CancellationToken cancellationToken)
        {
            // If the file does not exist, return an empty string and log a warning.
            bool exists = await Exists(pathOrFilename, cancellationToken);
            
            if (!exists)
            {
                Debug.LogWarning($"FileHandler: File does not exist at path or filename: {pathOrFilename}" +
                                 $"\nReturning empty string and no data will be loaded.");
                return string.Empty;
            }
                
            string fileContent = await File.ReadAllTextAsync(GetFullPath(pathOrFilename), cancellationToken);
            
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
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to erase.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task Erase(string pathOrFilename, CancellationToken cancellationToken)
            => await WriteFile(pathOrFilename, string.Empty, cancellationToken);

        /// <summary>
        /// Deletes a file at the given path or filename. This will remove the file from disk.
        /// Use <see cref="Erase(string, CancellationToken)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to delete.</param>
        public virtual void Delete(string pathOrFilename) 
            => File.Delete(GetFullPath(pathOrFilename));
    }
}
