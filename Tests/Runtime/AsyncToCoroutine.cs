using System;
using System.Collections;
using System.Threading.Tasks;

namespace Tests.Runtime
{
    /// <summary>
    /// Functions to allow writing a test as an async function, but run it under UnityTest as a coroutine.
    /// </summary>
    public class AsyncToCoroutine
    {
        public static IEnumerator AsCoroutine(Func<Task> taskFactory)
        {
            Task task = taskFactory();
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }
    }
}