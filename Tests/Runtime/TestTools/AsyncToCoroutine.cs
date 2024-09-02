using System;
using System.Collections;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Buck.SaveAsync.Tests
{
    /// <summary>
    /// Functions to allow writing a test as an async function, but run it under UnityTest as a coroutine.
    /// </summary>
    public class AsyncToCoroutine
    {
        public static IEnumerator AsCoroutine(Func<Task> taskFactory)
        {
            return new ToCoroutineEnumerator(taskFactory());
        }
        sealed class ToCoroutineEnumerator : IEnumerator
        {
            bool completed;
            Task task;
            bool isStarted = false;
            ExceptionDispatchInfo exception;

            public ToCoroutineEnumerator(Task task)
            {
                completed = false;
                this.task = task;
            }

            async void RunTask(Task task)
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    this.exception = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    completed = true;
                }
            }

            public object Current => null;

            public bool MoveNext()
            {
                if (!isStarted)
                {
                    isStarted = true;
                    RunTask(task);
                }

                if (exception != null)
                {
                    exception.Throw();
                    return false;
                }

                return !completed;
            }

            void IEnumerator.Reset()
            {
            }
        }
    }
}