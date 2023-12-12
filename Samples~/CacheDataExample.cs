using System;
using Buck.DataManagement;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Buck.DataManagementExample
{
    public class CacheDataExample : MonoBehaviour, ISaveable
    {
        // Things needed for this class to be ISaveable
        [SerializeField, HideInInspector] byte[] m_guidBytes;
        [ShowInInspector]
        public Guid Guid => new(ExtensionMethods.GetSerializableGuid(ref m_guidBytes));
        
        public string FileName => Files.SomeFile;

        // Your game data
        [SerializeField] string m_myString = "Some string!";
        [SerializeField] int m_myInt = 5;
        [SerializeField] float m_myFloat = 2f;

        [Serializable]
        public struct MySaveData
        {
            public string myString;
            public int myInt;
            public float myFloat;
        }

        MySaveData m_CachcedSaveDataState;
        
        void Awake()
        {
            // Register this ISaveable with the DataManager
            DataManager.RegisterSaveable(this);
        }
        
        [Button(ButtonSizes.Large)]
        public void CacheDataState()
        {
            m_CachcedSaveDataState = new MySaveData
            {
                myString = m_myString,
                myInt = m_myInt,
                myFloat = m_myFloat
            };
        }

        public object CaptureState() => m_CachcedSaveDataState;

        public void RestoreState(object state)
        {
            if (state is MySaveData s)
            {
                m_myString = s.myString;
                m_myInt = s.myInt;
                m_myFloat = s.myFloat;
            }
            else
            {
                throw new InvalidOperationException(gameObject.name + ".RestoreState() - state is not of correct type!");
            }
        }
    }
}
