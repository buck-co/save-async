using System;
using UnityEngine;
using Buck.DataManagement;
using TMPro;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Buck.DataManagementExample
{
    public class DataManagerBenchmarks : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI m_debugText;
        List<string> m_debugOutput = new ();
        StringBuilder m_stringBuilder = new ();
        string[] m_filenames =
        {
            Files.GameData,
            Files.SomeFile
        };
        
        async void AddDebugOutput(string output)
        {
            // If the list is too long, remove the oldest entry
            if (m_debugOutput.Count > 30)
                m_debugOutput.RemoveAt(0);
            
            // Append the current time to the output
            output = "> " + DateTime.Now.ToString("HH:mm:ss.fff") + ": " + output;
            
            m_debugOutput.Add(output);
            m_stringBuilder.Clear();
            for (int i = 0; i < m_debugOutput.Count; i++)
                m_stringBuilder.AppendLine(m_debugOutput[i]);
            
            // Switch back to the main thread to access the Unity API
            await Awaitable.MainThreadAsync();
            
            m_debugText.text = m_stringBuilder.ToString();
        }

        async Awaitable RunBenchmark(string testName, Awaitable test)
        {
            // Create a new GUID to track which save test is which
            Guid guid = Guid.NewGuid();
            string shortGuid = guid.ToString().Substring(0, 4);
            
            Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting " + testName + "() " + shortGuid + " ...");
            
            try
            {
                await test;
                AddDebugOutput(testName + "() " + shortGuid + " completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">" + testName + "() " + shortGuid + " failed: " + e.Message + "</color>");
            }
        }

        public void SaveGameData()
            => RunBenchmark("SaveGameData", DataManager.SaveAsync(m_filenames));
        
        public void LoadGameData()
            => RunBenchmark("LoadGameData", DataManager.LoadAsync(m_filenames));

        public void EraseGameData()
            => RunBenchmark("EraseGameData", DataManager.EraseAsync(m_filenames));
        
        public void DeleteGameData()
            => RunBenchmark("DeleteGameData", DataManager.DeleteAsync(m_filenames));
        
        public async void SaveQueueTest()
        {
            // Create a new GUID to track which save test is which
            Guid guid = Guid.NewGuid();
            string shortGuid = guid.ToString().Substring(0, 4);
            
            Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting SaveQueueTest() " + shortGuid + " ...");
            
            try
            {
                for (int i = 0; i < 100; i++)
                    DataManager.SaveAsync(new[] { Files.GameData, Files.SomeFile });

                while (DataManager.SaveQueueCount > 0)
                    await Awaitable.NextFrameAsync();

                AddDebugOutput("SaveQueueTest() " + shortGuid + " completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">SaveQueueTest() " + shortGuid + " failed: " + e.Message + "</color>");
            }
        }

        public async void LoadQueueTest()
        {
            // Create a new GUID to track which save test is which
            Guid guid = Guid.NewGuid();
            string shortGuid = guid.ToString().Substring(0, 4);
            
            Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting LoadQueueTest() " + shortGuid + " ...");
            
            try
            {
                for (int i = 0; i < 100; i++)
                    DataManager.LoadAsync(new[] { Files.GameData, Files.SomeFile });

                while (DataManager.LoadQueueCount > 0)
                    await Awaitable.NextFrameAsync();
                
                AddDebugOutput("LoadQueueTest() " + shortGuid + " completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">LoadQueueTest() " + shortGuid + " failed: " + e.Message + "</color>");
            }
        }
    }
}
