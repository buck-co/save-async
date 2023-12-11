using System;
using System.Collections.Generic;
using Buck.DataManagement;
using UnityEngine;

namespace Buck.DataManagementExample
{
    public class GameDataExample : MonoBehaviour, ISaveable
    {
        [Serializable]
        public struct MyCustomData
        {
            // Custom data fields
            public string playerName;
            public int playerHealth;
            public Vector3 position;
            public List<Enemy> enemies;
            public Dictionary<int, Item> inventory;
        }
        // Things needed for this class to be ISaveable
        [SerializeField, HideInInspector] byte[] m_guidBytes;
        public Guid Guid => new(m_guidBytes);
        void OnValidate() => ExtensionMethods.GetSerializableGuid(ref m_guidBytes);

        public string FileName => Files.GameData;

        public Type SaveDataType => typeof(MyCustomData);
        
        // Your game data
        [SerializeField] string m_playerName = "Steve";
        [SerializeField] int m_playerHealth = 100;
        [SerializeField] Vector3 m_position = new Vector3(1f, 2f, 3f);
        
        public struct Enemy
        {
            public string m_name;
            public int m_hitPoints;
        }
        List<Enemy> m_enemies = new List<Enemy>()
        {
            new()
            {
                m_name = "Goblin",
                m_hitPoints = 10
            },
            new()
            {
                m_name = "Orc",
                m_hitPoints = 20
            },
            new()
            {
                m_name = "Dragon",
                m_hitPoints = 100
            }
        };

        public struct Item
        {
            public string m_name;
            public int m_quantity;
        }
        Dictionary<int, Item> m_inventory = new()
        {
            {1, new Item() { m_name = "ItemOne",   m_quantity = 5 }},
            {2, new Item() { m_name = "ItemTwo",   m_quantity = 6 }},
            {3, new Item() { m_name = "ItemThree", m_quantity = 7 }}
        };

        void Awake()
        {
            // Register this ISaveable with the DataManager
            DataManager.RegisterSaveable(this);
        }
        
        /// <summary>
        /// One example implementation of the CaptureState method where
        /// the data is a snapshot of the current state of member variables.
        /// </summary>
        public object CaptureState()
        {
            return new MyCustomData
            {
                playerName = m_playerName,
                playerHealth = m_playerHealth,
                position = m_position,
                enemies = m_enemies,
                inventory = m_inventory
            };
        }

        public void RestoreState(object state)
        {
            var s = (MyCustomData)state;

            m_playerName = s.playerName;
            m_playerHealth = s.playerHealth;
            m_position = s.position;
            m_enemies = s.enemies;
            m_inventory = s.inventory;
        }
    }
}
