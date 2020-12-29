using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SharedKernel.Infrastructure.Events.InMemory
{
    /// <summary>
    /// 
    /// </summary>
    public class DomainEventsToExecute
    {
        /// <summary>
        /// 
        /// </summary>
        public readonly ConcurrentBag<Func<CancellationToken, Task>> Subscribers = new();
    }
}
