// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

namespace Buck.SaveAsync
{
    public class SaveAsyncMenu
    {
        [MenuItem("Tools/Save Async/Open Persistent Data Path", false, 0)]
        static void OpenPersistentDataPath()
        {
            // Get the path to the persistent data directory
            string path = Application.persistentDataPath;

            // Open the directory in the file explorer
            Process.Start(path);
        }

        [MenuItem("Tools/Save Async/Clear Persistent Data Path", false, 1)]
        static void ClearPersistentDataPath()
        {
            // Get the path to the persistent data directory
            string path = Application.persistentDataPath;

            // Confirm with the user that they want to delete all data
            if (EditorUtility.DisplayDialog("Clear Persistent Data",
                    "Are you sure you want to delete all data in the persistent data path?",
                    "Yes", "No"))
            {
                // Delete all files and directories at the path
                DirectoryInfo directory = new DirectoryInfo(path);
                foreach (FileInfo file in directory.GetFiles())
                    file.Delete();

                foreach (DirectoryInfo dir in directory.GetDirectories())
                    dir.Delete(true);

                // Optional: Show a confirmation message
                EditorUtility.DisplayDialog("Persistent Data Cleared",
                    "All data in the persistent data path has been deleted.",
                    "Ok");
            }
        }
    }
}