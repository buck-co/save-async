using System;
using UnityEngine;
using Buck.DataManagement;
using TMPro;
using System.Collections.Generic;
using System.Text;

namespace Buck.DataManagementExample
{
    public class SaveTests : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI m_debugText;
        List<string> m_debugOutput = new List<string>();
        StringBuilder m_stringBuilder = new StringBuilder();
        
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
            
            await Awaitable.MainThreadAsync();
            
            m_debugText.text = m_stringBuilder.ToString();
        }
        
        public async void SaveGameData()
        {
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting SaveGameData()...");
            
            try
            {
                await DataManager.SaveAsync(new[] { Files.GameData, Files.SomeFile });
                AddDebugOutput("SaveGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">SaveGameData() failed: " + e.Message + "</color>");
            }

            stopwatch.Stop();
            stopwatch.Reset();
        }

        public async void SaveQueueTest()
        {
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting SaveQueueTest()...");
            
            try
            {
                for (int i = 0; i < 100; i++)
                    await DataManager.SaveAsync(new[] { Files.GameData, Files.SomeFile });
                AddDebugOutput("SaveQueueTest() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">SaveQueueTest() failed: " + e.Message + "</color>");
            }
            
            stopwatch.Stop();
            stopwatch.Reset();
        }
        
        public async void EraseGameData()
        {
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting EraseGameData()...");
            
            try
            {
                await DataManager.EraseAsync(new[] { Files.GameData, Files.SomeFile });
                AddDebugOutput("EraseGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">EraseGameData() failed: " + e.Message + "</color>");
            }
            
            stopwatch.Stop();
            stopwatch.Reset();
        }
        
        public async void DeleteGameData()
        {
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting DeleteGameData()...");
            
            try
            {
                await DataManager.DeleteAsync(new[] { Files.GameData, Files.SomeFile });
                AddDebugOutput("DeleteGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">DeleteGameData() failed: " + e.Message + "</color>");
            }
            
            stopwatch.Stop();
            stopwatch.Reset();
        }

        public async void LoadGameData()
        {
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting LoadGameData()...");
            
            try
            {
                await DataManager.LoadAsync(new[] { Files.GameData, Files.SomeFile });
                AddDebugOutput("LoadGameData() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">LoadGameData() failed: " + e.Message + "</color>");
            }
            
            stopwatch.Stop();
            stopwatch.Reset();
        }

        public async void LoadQueueTest()
        {
            System.Diagnostics.Stopwatch stopwatch = new();
            stopwatch.Start();
            AddDebugOutput("Starting LoadQueueTest()...");
            
            try
            {
                for (int i = 0; i < 100; i++)
                    await DataManager.LoadAsync(new[] { Files.GameData, Files.SomeFile });
                AddDebugOutput("LoadQueueTest() completed in " + stopwatch.ElapsedMilliseconds + "ms");
            }
            catch (Exception e)
            {
                AddDebugOutput("<color=\"red\">LoadQueueTest() failed: " + e.Message + "</color>");
            }
            
            stopwatch.Stop();
            stopwatch.Reset();
        }
    }
}
