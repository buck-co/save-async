// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

/*
 * This example implementation of the CaptureState method shows how the CaptureState method
 * can be used to create an instant snapshot of the current values of the object's fields.
 * Contrast this with the CacheDataExample class, which shows how data can be cached manually.
 */

using System;
using System.Collections.Generic;
using Buck.SaveAsync;
using UnityEngine;

namespace Buck.SaveAsyncExample
{
    public class GameDataExample : MonoBehaviour, ISaveable<GameDataExample.MyCustomData>
    {
        // ISaveable needs a unique string "Key" which is used to identify the object in the save data.
        // For an example that uses a Guid, see CacheDataExample.cs.
        public string Key => "GameDataExample";
        public string Filename => Files.GameData;
        
        public int FileVersion => 1;

        [Serializable]
        public struct Item
        {
            public string m_name;
            public int m_quantity;
        }

        // Your game data should go in a serializable struct
        [Serializable]
        public struct MyCustomData
        {
            public string playerName;
            public int playerHealth;
            public Vector3 position;
            public Dictionary<int, Item> inventory;
        }
        
        [SerializeField] string m_playerName = "The Player Name";
        [SerializeField] int m_playerHealth = 100;
        [SerializeField] Vector3 m_position = new Vector3(1f, 2f, 3f);
        
        Dictionary<int, Item> m_inventory = new()
        {
            {1, new Item() { m_name = "ItemOne",   m_quantity = 5 }},
            {2, new Item() { m_name = "ItemTwo",   m_quantity = 6 }},
            {3, new Item() { m_name = "ItemThree", m_quantity = 7 }}
        };

        void Awake()
        {
            // Register this ISaveable
            SaveManager.RegisterSaveable(this);
        }
        
        // Every ISaveable must implement the CaptureState and RestoreState method
        // CaptureState is called when the game is saved with SaveManager.Save()
        // RestoreState is called when the game is loaded with SaveManager.Load()
        public MyCustomData CaptureState()
        {
            return new MyCustomData
            {
                playerName = m_playerName,
                playerHealth = m_playerHealth,
                position = m_position,
                inventory = m_inventory
            };
        }

        public void RestoreState(MyCustomData state)
        {
            // When no save exists, SaveManager passes default(MyCustomData).
            m_playerName = string.IsNullOrEmpty(state.playerName) ? "The Player Name" : state.playerName;
            m_playerHealth = state.playerHealth == 0 ? 100 : state.playerHealth;
            m_position = state.position == default ? new Vector3(1f, 2f, 3f) : state.position;

            if (state.inventory != null && state.inventory.Count > 0)
            {
                m_inventory = state.inventory;
            }
            else
            {
                m_inventory = new()
                {
                    {1, new Item() { m_name = "ItemOne",   m_quantity = 5 }},
                    {2, new Item() { m_name = "ItemTwo",   m_quantity = 6 }},
                    {3, new Item() { m_name = "ItemThree", m_quantity = 7 }}
                };
            }
        }
    }
}
