// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using System;
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
        protected virtual string FilenameSuffix
#if UNITY_EDITOR
            => "_editor";
#else
            => string.Empty;
#endif
        
        /// <summary>
        /// The file extension to use for the files. By default, it is ".dat".
        /// </summary>
        protected virtual string FileExtension => ".dat";
        
        protected virtual void OnEnable()
            => m_persistentDataPath = Application.persistentDataPath;
        
        /// <summary>
        /// Validates that the given path or filename is safe and well-formed.
        /// Throws an exception if the path contains invalid characters, is absolute, or attempts directory traversal.
        /// </summary>
        /// <param name="pathOrFilename">The path or filename to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the path is null, empty, whitespace, contains "..", is absolute, or contains invalid characters.</exception>
        protected virtual void ValidatePath(string pathOrFilename)
        {
            if (string.IsNullOrWhiteSpace(pathOrFilename))
                throw new ArgumentException("[Save Async] FileHandler.ValidatePath() - Path or filename cannot be null, empty, or whitespace.", nameof(pathOrFilename));

            // Prevent directory traversal
            if (pathOrFilename.Contains("..") || Path.IsPathRooted(pathOrFilename))
                throw new ArgumentException("[Save Async] FileHandler.ValidatePath() - Path contains invalid characters or is absolute.", nameof(pathOrFilename));

            // Check for invalid path characters instead of filename characters
            if (pathOrFilename.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new ArgumentException("[Save Async] FileHandler.ValidatePath() - Path contains invalid characters.", nameof(pathOrFilename));
        }
        
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
            ValidatePath(pathOrFilename);
            
            string path = $"{pathOrFilename}{FilenameSuffix}{FileExtension}";
            
            if (SaveManager.SaveSlotIndex > -1)
                return Path.Combine($"slot{SaveManager.SaveSlotIndex}", path);

            return path;
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
        /// <returns>True if the file exists; otherwise, false.</returns>
        public virtual bool Exists(string pathOrFilename)
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
        {
            string fullPath = GetFullPath(pathOrFilename);
    
            // Get the directory path from the full file path
            string directoryPath = Path.GetDirectoryName(fullPath);
    
            // Create the directory structure if it doesn't exist
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);
    
            await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Writes the given content to a file at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to write.</param>
        /// <param name="content">The string to write to the file.</param>
        public virtual async Task WriteFile(string pathOrFilename, string content)
            => await WriteFile(pathOrFilename, content, CancellationToken.None);

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
            try
            {
                string fullPath = GetFullPath(pathOrFilename);
                
                // If the file does not exist, return an empty string and log a warning.
                if (!Exists(pathOrFilename))
                {
                    Debug.LogWarning($"[Save Async] FileHandler.ReadFile() - File does not exist at path \"{fullPath}\". This may be expected if the file has not been created yet.");
                    return string.Empty;
                }
                
                string fileContent = await File.ReadAllTextAsync(GetFullPath(pathOrFilename), cancellationToken).ConfigureAwait(false);
            
                // If the file is empty, return an empty string and log a warning.
                if (string.IsNullOrEmpty(fileContent))
                {
                    if (SaveManager.SaveSlotIndex > -1)
                        Debug.LogWarning($"[Save Async] FileHandler.ReadFile() - The file \"{pathOrFilename}\" in slot index {SaveManager.SaveSlotIndex} was empty. This may be expected if the file has been erased.");
                    else
                        Debug.LogWarning($"[Save Async] FileHandler.ReadFile() - The file \"{pathOrFilename}\" was empty. This may be expected if the file has been erased.");
                    
                    return string.Empty;
                }
                
                return fileContent;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogError($"[Save Async] FileHandler.ReadFile() - Access denied to file \"{pathOrFilename}\": {ex.Message}");
                return string.Empty;
            }
            catch (IOException ex)
            {
                Debug.LogError($"[Save Async] FileHandler.ReadFile() - IO error reading file \"{pathOrFilename}\": {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Returns the contents of a file at the given path or filename.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to read.</param>
        public virtual async Task<string> ReadFile(string pathOrFilename)
            => await ReadFile(pathOrFilename, CancellationToken.None);
        
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
        /// Erases a file at the given path or filename. The file will still exist on disk, but it will be empty.
        /// Use <see cref="Delete(string)"/> to remove the file from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to erase.</param>
        public virtual async Task Erase(string pathOrFilename)
            => await Erase(pathOrFilename, CancellationToken.None);

        /// <summary>
        /// Deletes a file at the given path or filename. This will remove the file from disk.
        /// Use <see cref="Erase(string, CancellationToken)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to delete.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task Delete(string pathOrFilename, CancellationToken cancellationToken)
        {
            string fullPath = GetFullPath(pathOrFilename);
            if (File.Exists(fullPath))
                await Task.Run(() => File.Delete(fullPath), cancellationToken).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Deletes a file at the given path or filename. This will remove the file from disk.
        /// Use <see cref="Erase(string, CancellationToken)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile"
        /// Path example: "MyFolder/MyFile"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to delete.</param>
        public virtual async Task Delete(string pathOrFilename)
            => await Delete(pathOrFilename, CancellationToken.None);
    }
}