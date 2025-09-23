// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

/*
 * This example implementation of the ISaveable<TState> interface shows how data can be cached manually.
 * The CaptureState method returns the cached data. Contrast this with the GameDataExample class,
 * which shows how to create a snapshot of current values.
 * 
 * In Unity, use the "Cache Data State" context menu item to update the cached data.
 */

using System;
using Buck.SaveAsync;
using UnityEngine;

namespace Buck.SaveAsyncExample
{
    public class CacheDataExample : MonoBehaviour, ISaveable<CacheDataExample.MySaveData>
    {
        // ISaveable needs a unique string "Key" which is used to identify the object in the save data.
        // This is can optionally be a serialized byte array that does not change.
        // Use OnValidate to ensure that your ISaveable's Guid has a value when the MonoBehaviour is created.
        // For an example that uses a string, see GameDataExample.cs.
        [SerializeField, HideInInspector] byte[] m_guidBytes;
        public string Key => new Guid(m_guidBytes).ToString();
        void OnValidate() => SaveManager.GetSerializableGuid(ref m_guidBytes);

        public string Filename => Files.SomeFile;
        
        public int FileVersion => 1;

        [Serializable]
        public struct MySaveData
        {
            public string myString;
            public int myInt;
            public float myFloat;
        }
        
        [SerializeField] string m_myString = "Some string!";
        [SerializeField] int m_myInt = 5;
        [SerializeField] float m_myFloat = 2f;


        MySaveData m_cachedSaveDataState;
        
        void Awake()
        {
            SaveManager.RegisterSaveable(this);
        }
        
        // Use the "Cache Data State" context menu item to update the cached data.
        // This is to simulate a situation where the data is cached at a specific time
        // that is not necessarily when the game is saved.
        [ContextMenu("Cache Data State")]
        public void CacheDataState()
        {
            m_cachedSaveDataState = new MySaveData
            {
                myString = m_myString,
                myInt = m_myInt,
                myFloat = m_myFloat
            };
        }
        
        // CaptureState is called when the game is saved with SaveManager.Save()
        // RestoreState is called when the game is loaded with SaveManager.Load()
        public MySaveData CaptureState() => m_cachedSaveDataState;

        public void RestoreState(MySaveData state)
        {
            if (!string.IsNullOrEmpty(state.myString))
            {
                m_myString = state.myString;
                m_myInt = state.myInt;
                m_myFloat = state.myFloat;
            }
            else
            {
                Debug.Log("CacheDataExample: RestoreState called without prior cached data. Initializing default values.");
                m_myString = "Default string";
                m_myInt = 5;
                m_myFloat = 2f;
            }
        }
    }
}
