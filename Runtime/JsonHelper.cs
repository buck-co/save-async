using UnityEngine;

namespace Buck.DataManagement
{
    /// <summary>
    /// Wrapper class for serializing and deserializing arrays using Unity's built-in JsonUtility.
    /// Source: https://stackoverflow.com/questions/36239705/serialize-and-deserialize-json-and-json-array-in-unity
    /// </summary>
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
            return wrapper.Items;
        }

        public static string ToJson<T>(T[] array)
        {
            Wrapper<T> wrapper = new Wrapper<T>();
            wrapper.Items = array;
            return JsonUtility.ToJson(wrapper);
        }

        public static string ToJson<T>(T[] array, bool prettyPrint)
        {
            Wrapper<T> wrapper = new Wrapper<T>();
            wrapper.Items = array;
            return JsonUtility.ToJson(wrapper, prettyPrint);
        }

        [System.Serializable]
        class Wrapper<T>
        {
            public T[] Items;
        }
    }
}
