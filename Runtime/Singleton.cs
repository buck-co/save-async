// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using UnityEngine;

namespace Buck.SaveAsync
{
    /// <summary>
    /// Inherit from this base class to create a singleton.
    /// e.g. public class MyClassName : Singleton<MyClassName> {}
    /// </summary>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        // Check to see if we're about to be destroyed.
        static bool m_ShuttingDown = false;
        static object m_Lock = new object();
        static T m_Instance;

        /// <summary>
        /// Access singleton instance through this property.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (m_ShuttingDown)
                {
                    Debug.LogWarning("[SaveAsync] Singleton.Instance '" + typeof(T) +
                        "' already destroyed. Returning null.");
                    return null;
                }

                lock (m_Lock)
                {
                    if (m_Instance == null)
                    {
                        // Search for existing instance.
                        m_Instance = (T)FindAnyObjectByType(typeof(T));

                        // Create new instance if one doesn't already exist.
                        if (m_Instance == null)
                        {
                            // Need to create a new GameObject to attach the singleton to.
                            var singletonObject = new GameObject();
                            m_Instance = singletonObject.AddComponent<T>();
                            singletonObject.name = typeof(T).ToString() + " (Singleton)";

                            // Make instance persistent.
                            DontDestroyOnLoad(singletonObject);
                        }
                    }

                    return m_Instance;
                }
            }
        }

        void OnApplicationQuit()
            => m_ShuttingDown = true;

        void OnDestroy()
            => m_ShuttingDown = true;
    }
}