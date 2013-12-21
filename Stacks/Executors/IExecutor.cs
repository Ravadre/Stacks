using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Executors
{
    public interface IExecutor
    {
        void Enqueue(Action action);

        Task Stop();
        Task Completion { get; }

        SynchronizationContext Context { get; }
    }
}
