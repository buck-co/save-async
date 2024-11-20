// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

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
        string m_persistentDataPath;
        
        protected virtual void OnEnable() =>
            m_persistentDataPath = Application.persistentDataPath;

        
        /// <summary>
        /// Returns the full path to a file in the persistent data path using the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file that will be combined with the persistent data path.</param>
        protected virtual string GetPath(string pathOrFilename)
            => Path.Combine(m_persistentDataPath, pathOrFilename);

        /// <summary>
        /// For user or profile specific files, we want to use a modified file name only on IO functions.
        /// Otherwise all other calls should use an unmodified filename.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        protected virtual string GetModifiedFileName(string fileName) => fileName;

        /// <summary>
        /// Returns true if a file exists at the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to check.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task<ExistsResult> Exists(string pathOrFilename, CancellationToken cancellationToken)
        {
            var result = new ExistsResult
            {
                Local = File.Exists(GetPath(GetModifiedFileName(pathOrFilename)))
            };
            
            // If using a remote service, override this method in a derived class and set Remote to true if the file exists.
            result.Remote = false;
            
            return result;
        }

        /// <summary>
        /// Writes the given content to a file at the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to write.</param>
        /// <param name="content">The string to write to the file.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task WriteFile(string pathOrFilename, string content, CancellationToken cancellationToken)
        {
            pathOrFilename = GetModifiedFileName(pathOrFilename);
            await File.WriteAllTextAsync(GetPath(pathOrFilename), content, cancellationToken);
        }

        public class ReadResult
        {
            public string Local = "";
            public string Remote = "";
            public bool NetworkError;
        }
        
        public class ExistsResult
        {
            public bool Local;
            public bool Remote;
        }
        /// <summary>
        /// Returns the contents of a file at the given path or filename.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to read.</param>
        /// <param name="cancellationToken">The cancellation token should be the same one from the calling MonoBehaviour.</param>
        public virtual async Task<ReadResult> ReadFile(string pathOrFilename, CancellationToken cancellationToken)
        {
            // If the file does not exist, return an empty string and log a warning.
            var exists = await Exists(pathOrFilename, cancellationToken);
            
            // Exists() should handle it's own modification of the pathOrFilename, so we don't want to alter it until we make that call.
            pathOrFilename = GetModifiedFileName(pathOrFilename);
            var result = new ReadResult();
            if (!exists.Local)
            {
                Debug.LogWarning($"FileHandler: File does not exist at path or filename: {pathOrFilename}" +
                                 $"\nReturning empty string and no data will be loaded.");
                return result;
            }
            result.Local = await File.ReadAllTextAsync(GetPath(pathOrFilename), cancellationToken);
            
            // If the file is empty, return an empty string and log a warning.
            if (string.IsNullOrEmpty(result.Local))
            {
                Debug.LogWarning($"FileHandler: Attempted to load {pathOrFilename} but the file was empty.");
            }

            return result;
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
        public virtual async Task Erase(string pathOrFilename, CancellationToken cancellationToken)
            => await WriteFile(GetModifiedFileName(pathOrFilename), string.Empty, cancellationToken);

        /// <summary>
        /// Deletes a file at the given path or filename. This will remove the file from disk.
        /// Use <see cref="Erase(string, CancellationToken)"/> to fill the file with an empty string without removing it from disk.
        /// <code>
        /// File example: "MyFile.dat"
        /// Path example: "MyFolder/MyFile.dat"
        /// </code>
        /// </summary>
        /// <param name="pathOrFilename">The path or filename of the file to delete.</param>
        public virtual void Delete(string pathOrFilename) 
            => File.Delete(GetPath(GetModifiedFileName(pathOrFilename)));
    }
}
