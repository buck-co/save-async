// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using UnityEngine;
using UnityEditor;
using System.IO;

namespace Buck.SaveAsync
{
    /// <summary>
    /// Unity Editor utilities for managing Save Async persistent data during development.
    /// </summary>
    public static class SaveAsyncMenu
    {
        [MenuItem("Tools/Save Async/Open Persistent Data Path", false, 0)]
        static void OpenPersistentDataPath()
        {
            // Get the path to the persistent data directory
            string path = Application.persistentDataPath;
            
            // Ensure the directory exists
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
    
            // Open the directory in a cross-platform way
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/Save Async/Clear Persistent Data Path", false, 1)]
        static void ClearPersistentDataPath()
        {
            string path = Application.persistentDataPath;

            if (!EditorUtility.DisplayDialog("Clear Persistent Data",
                    $"Are you sure you want to delete all data in:\n{path}",
                    "Yes", "No")) return;
            try
            {
                DirectoryInfo directory = new DirectoryInfo(path);
            
                if (!directory.Exists)
                {
                    EditorUtility.DisplayDialog("Directory Not Found",
                        "The persistent data path does not exist.",
                        "Ok");
                    return;
                }
            
                foreach (FileInfo file in directory.GetFiles())
                    file.Delete();

                foreach (DirectoryInfo dir in directory.GetDirectories())
                    dir.Delete(true);

                EditorUtility.DisplayDialog("Persistent Data Cleared",
                    "All data in the persistent data path has been deleted.",
                    "Ok");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to clear persistent data:\n{e.Message}",
                    "Ok");
            }
        }
        
        [MenuItem("Tools/Save Async/Clear Editor Save Files", false, 2)]
        static void ClearEditorSaveFiles()
        {
            // Get the path to the persistent data directory
            string path = Application.persistentDataPath;

            // Confirm with the user that they want to delete editor save files
            if (!EditorUtility.DisplayDialog("Clear Editor Save Files",
                    "Are you sure you want to delete all save files with '_editor' suffix?\n\n" +
                    "This will only delete files created while testing in the Unity Editor.",
                    "Yes", "No")) return;
            try
            {
                DirectoryInfo directory = new DirectoryInfo(path);
                    
                if (!directory.Exists)
                {
                    EditorUtility.DisplayDialog("Directory Not Found",
                        "The persistent data path does not exist.",
                        "Ok");
                    return;
                }
                    
                // Find and delete all files containing "_editor" (before the file extension)
                FileInfo[] allFiles = directory.GetFiles("*_editor*", SearchOption.AllDirectories);
                    
                int deletedCount = 0;
                foreach (FileInfo file in allFiles)
                {
                    if (file.Name.Contains("_editor"))
                    {
                        file.Delete();
                        deletedCount++;
                    }
                }

                // Show confirmation with count
                EditorUtility.DisplayDialog("Editor Files Cleared",
                    $"Deleted {deletedCount} editor save file{(deletedCount == 1 ? "" : "s")}.",
                    "Ok");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to clear editor save files:\n{e.Message}",
                    "Ok");
            }
        }
    }
}