using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Stacks.Executors
{
    public class ActionBlockExecutor : SynchronizationContext, IExecutor
    {
        private ActionBlock<Action> queue;
        private string name;

        public Task Completion { get { return queue.Completion; } }

        public event Action<Exception> Error;

        public ActionBlockExecutor(string name, ActionContextExecutorSettings settings)
        {
            this.name = name;
            this.queue = new ActionBlock<Action>(a =>
            {
                ExecuteAction(a);
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = settings.QueueBoundedCapacity,
                MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism
            });
        }

        private void ExecuteAction(Action a)
        {
            var oldCtx = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(this);
            try
            {
                a();
            }
            catch (Exception e)
            {
                ErrorOccured(e);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldCtx);
            }
        }

        private void ErrorOccured(Exception e)
        {
            OnError(e);
            this.queue.Complete();
        }

        private void OnError(Exception e)
        {
            var h = Error;
            if (h != null)
            {
                try { h(e); }
                catch { }
            }
        }

        public void Enqueue(Action action)
        {
            if (!queue.Post(action))
                queue.SendAsync(action).Wait();
       }

        public Task Stop()
        {
            queue.Complete();
            return queue.Completion;
        }

        public SynchronizationContext Context
        {
            get { return this; }
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            Enqueue(() => d(state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException();
        }

        public override string ToString()
        {
            return "ActionBlock Executor " +
                (name == null ? "" : string.Format("({0})", name));
        }
    }
}
