// MIT License - Copyright (c) 2023 BUCK Design LLC - https://github.com/buck-co

using System;
using UnityEngine;
using Buck.SaveAsync;
using TMPro;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Buck.SaveAsyncExample
{
    public class Benchmarks : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI m_debugText;
        List<string> m_debugOutput = new ();
        StringBuilder m_stringBuilder = new ();
        string[] m_filenames =
        {
            Files.GameData,
            Files.SomeFile
        };
        
        enum BenchmarkType
        {
            SaveGameData,
            LoadGameData,
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
                            await SaveManager.Save(m_filenames);
                            break;
                        case BenchmarkType.LoadGameData:
                            await SaveManager.Load(m_filenames);
                            break;
                        case BenchmarkType.EraseGameData:
                            await SaveManager.Erase(m_filenames);
                            break;
                        case BenchmarkType.DeleteGameData:
                            await SaveManager.Delete(m_filenames);
                            break;
                    }
                    
                    // Switch back to the main thread while waiting
                    Awaitable.MainThreadAsync();
                    
                    while (SaveManager.IsBusy)
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

        public void LoadGameData()
            => RunBenchmark(BenchmarkType.LoadGameData);

        public void EraseGameData()
            => RunBenchmark(BenchmarkType.EraseGameData);
        
        public void DeleteGameData()
            => RunBenchmark(BenchmarkType.DeleteGameData);
    }
}
