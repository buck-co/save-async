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
            readonly Task m_task;
            bool m_completed;
            bool m_isStarted = false;
            ExceptionDispatchInfo m_exception;

            public ToCoroutineEnumerator(Task task)
            {
                m_completed = false;
                this.m_task = task;
            }

            async void RunTask(Task task)
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    this.m_exception = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    m_completed = true;
                }
            }

            public object Current => null;

            public bool MoveNext()
            {
                if (!m_isStarted)
                {
                    m_isStarted = true;
                    RunTask(m_task);
                }

                if (m_exception != null)
                {
                    m_exception.Throw();
                    return false;
                }

                return !m_completed;
            }

            void IEnumerator.Reset() { }
        }
    }
}