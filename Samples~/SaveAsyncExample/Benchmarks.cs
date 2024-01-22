// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using System;
using UnityEngine;
using Buck.SaveAsync;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine.UI;

namespace Buck.SaveAsyncExample
{
    public enum BenchmarkType
    {
        SaveGameData,
        LoadGameData,
        EraseGameData,
        DeleteGameData
    }
    
    public class Benchmarks : MonoBehaviour
    {
        [SerializeField] Text m_debugText;
        List<string> m_debugOutput = new ();
        StringBuilder m_stringBuilder = new ();
        
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

        async Awaitable RunBenchmark(BenchmarkType benchmarkType, string[] filenames)
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
                            await SaveManager.Save(filenames);
                            break;
                        case BenchmarkType.LoadGameData:
                            await SaveManager.Load(filenames);
                            break;
                        case BenchmarkType.EraseGameData:
                            await SaveManager.Erase(filenames);
                            break;
                        case BenchmarkType.DeleteGameData:
                            await SaveManager.Delete(filenames);
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

        public async Awaitable SaveGameData(string[] filenames)
            => await RunBenchmark(BenchmarkType.SaveGameData, filenames);

        public async Awaitable LoadGameData(string[] filenames)
            => await RunBenchmark(BenchmarkType.LoadGameData, filenames);

        public async Awaitable EraseGameData(string[] filenames)
            => await RunBenchmark(BenchmarkType.EraseGameData, filenames);
        
        public async Awaitable DeleteGameData(string[] filenames)
            => await RunBenchmark(BenchmarkType.DeleteGameData, filenames);
    }
}
