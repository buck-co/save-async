// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

/*
 * This example implementation of the ISaveable interface shows how data can be cached manually.
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
    public class CacheDataExample : MonoBehaviour, ISaveable
    {
        // ISaveable needs a Guid which is used to identify the object in the save data.
        // This is typically a serialized byte array that does not change.
        // Use OnValidate to ensure that your ISaveable's Guid has a value when the MonoBehaviour is created.
        [SerializeField, HideInInspector] byte[] m_guidBytes;
        public Guid Guid => new(m_guidBytes);
        void OnValidate() => SaveManager.GetSerializableGuid(ref m_guidBytes);
        public string Filename => Files.SomeFile;

        // Your game data should go in a serializable struct
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


        MySaveData m_CachcedSaveDataState;
        
        void Awake()
        {
            // Register this ISaveable
            SaveManager.RegisterSaveable(this);
        }
        
        // Use the "Cache Data State" context menu item to update the cached data.
        // This is to simulate a situation where the data is cached at a specific time
        // that is not necessarily when the game is saved.
        // (e.g. when the player reaches a checkpoint or makes changes to their inventory)
        [ContextMenu("Cache Data State")]
        public void CacheDataState()
        {
            m_CachcedSaveDataState = new MySaveData
            {
                myString = m_myString,
                myInt = m_myInt,
                myFloat = m_myFloat
            };
        }
        
        // Every ISaveable must implement the CaptureState and RestoreState method
        // CaptureState is called when the game is saved with SaveManager.Save()
        // RestoreState is called when the game is loaded with SaveManager.Load()
        public object CaptureState() => m_CachcedSaveDataState;

        public void RestoreState(object state)
        {
            var s = (MySaveData)state;
            
            m_myString = s.myString;
            m_myInt = s.myInt;
            m_myFloat = s.myFloat;
        }
    }
}
