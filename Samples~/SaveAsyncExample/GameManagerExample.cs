// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using UnityEngine;
using Buck.SaveAsync;

namespace Buck.SaveAsyncExample
{
    public class GameManagerExample : MonoBehaviour
    {
        [SerializeField] bool m_runBenchmarks = true;

        string[] m_filenames =
        {
            Files.GameData,
            Files.SomeFile
        };
        
        Benchmarks m_benchmarks;

        void Awake()
        {
            m_benchmarks = GetComponent<Benchmarks>();
        }

        public async void SaveGameData()
        {
            if (m_runBenchmarks)
                await m_benchmarks.SaveGameData(m_filenames);
            else
                await SaveManager.Save(m_filenames);
        }
        
        public async void LoadGameData()
        {
            if (m_runBenchmarks)
                await m_benchmarks.LoadGameData(m_filenames);
            else
                await SaveManager.Load(m_filenames);
        }
        
        public async void EraseGameData()
        {
            if (m_runBenchmarks)
                await m_benchmarks.EraseGameData(m_filenames);
            else
                await SaveManager.Erase(m_filenames);
        }
        
        public async void DeleteGameData()
        {
            if (m_runBenchmarks)
                await m_benchmarks.DeleteGameData(m_filenames);
            else
                await SaveManager.Delete(m_filenames);
        }
    }
}