using System;
using System.Threading;
using UnityEngine;

namespace Tests.Runtime
{
    public class AwaitableExtensions
    {
        public static Awaitable Delay(TimeSpan time, CancellationToken cancellationToken)
        {
            return Awaitable.WaitForSecondsAsync((float)time.TotalSeconds, cancellationToken);
        }
    }
}