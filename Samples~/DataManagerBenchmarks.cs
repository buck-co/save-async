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
            Files.GameData
        };
        
        enum BenchmarkType
        {
            SaveGameData,
            LoadGameData,
            SaveGameDataQueue,
            LoadGameDataQueue,
            EraseGameData,
            DeleteGameData
        }
        
        void AddDebugOutput(string output)
        {
            // If the list is too long, remove the oldest entry
            if (m_debugOutput.Count > 30)
                m_debugOutput.RemoveAt(0);
            
            // Append the current time to the output
            output = "> " + DateTime.Now.ToString("HH:mm:ss.fff") + ": " + output;
            
            m_debugOutput.Add(output);
            m_stringBuilder.Clear();
            foreach (var t in m_debugOutput)
                m_stringBuilder.AppendLine(t);
            
            m_debugText.text = m_stringBuilder.ToString();
        }

        async Awaitable RunBenchmark(BenchmarkType benchmarkType)
        {
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                // Create a new GUID to track which save test is which
                Guid guid = Guid.NewGuid();
                string shortGuid = guid.ToString().Substring(0, 4);

                Stopwatch stopwatch = new();
                stopwatch.Start();
                AddDebugOutput("Starting " + benchmarkType + "() " + shortGuid + " ...");

                try
                {
                    switch (benchmarkType)
                    {
                        case BenchmarkType.SaveGameData:
                            await DataManager.SaveAsync(m_filenames);
                            break;
                        case BenchmarkType.LoadGameData:
                            await DataManager.LoadAsync(m_filenames);
                            break;
                        case BenchmarkType.SaveGameDataQueue:
                            for (int i = 0; i < 10; i++) DataManager.SaveAsync(m_filenames);
                            break;
                        case BenchmarkType.LoadGameDataQueue:
                            for (int i = 0; i < 10; i++) DataManager.LoadAsync(m_filenames);
                            break;
                        case BenchmarkType.EraseGameData:
                            await DataManager.EraseAsync(m_filenames);
                            break;
                        case BenchmarkType.DeleteGameData:
                            await DataManager.DeleteAsync(m_filenames);
                            break;
                    }
                    
                    // Switch back to the main thread while waiting
                    Awaitable.MainThreadAsync();
                    
                    while (DataManager.IsBusy)
                        await Awaitable.NextFrameAsync();
                    
                    AddDebugOutput(benchmarkType + "() " + shortGuid + " completed in " + stopwatch.ElapsedMilliseconds +
                                   "ms");
                }
                catch (Exception e)
                {
                    Awaitable.MainThreadAsync();
                    
                    AddDebugOutput("<color=\"red\">" + benchmarkType + "() " + shortGuid + " failed: " + e.Message +
                                   "</color>");
                }

                return;
            }
        }

        public void SaveGameData()
            => RunBenchmark(BenchmarkType.SaveGameData);
        
        public void SaveGameDataQueue()
            => RunBenchmark(BenchmarkType.SaveGameDataQueue);

        public void LoadGameData()
            => RunBenchmark(BenchmarkType.LoadGameData);
        
        public void LoadGameDataQueue()
            => RunBenchmark(BenchmarkType.LoadGameDataQueue);

        public void EraseGameData()
            => RunBenchmark(BenchmarkType.EraseGameData);
        
        public void DeleteGameData()
            => RunBenchmark(BenchmarkType.DeleteGameData);
    }
}
