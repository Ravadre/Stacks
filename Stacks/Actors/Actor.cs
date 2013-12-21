using Stacks.Executors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public class Actor
    {
        private readonly ActorContext context;
        private readonly string name;

        public Actor()
            : this(null, 
                   new ActionBlockExecutor(null, ActionContextExecutorSettings.Default))
        { }

        public Actor(string name)
            : this(name, 
                   new ActionBlockExecutor(name, ActionContextExecutorSettings.Default))
        { }

        public Actor(string name, IExecutor executor)
        {
            this.name = name;
            this.context = new ActorContext(name, executor);
        }

        protected Task Completion { get { return context.Completion; } }

        protected Task Stop()
        {
            return context.Stop();
        }

        public bool Named { get { return name != null; } }
        public string Name { get { return name; } }

        protected IActorContext Context { get { return context; } }
        protected SynchronizationContext GetActorSynchronizationContext()
        {
            return context.Context;
        }
    }

}
