using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class ActorWrapperBase : IActor
    {
        protected readonly Actor actorImplementation;
        public string Name => actorImplementation.Name;
        public IActor Parent => actorImplementation.Parent;
        public IEnumerable<IActor> Childs => actorImplementation.Childs; 

        public ActorWrapperBase(Actor actorImplementation)
        {
            this.actorImplementation = actorImplementation;
        }

        protected Task<T> HandleException<T>(Task<T> task)
        {
            return task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    actorImplementation.ErrorOccuredInMethod("", t.Exception);
                    throw t.Exception.InnerException;
                }
                return t.Result;
            });
        }

        protected Task HandleException(Task task)
        {
            return task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    actorImplementation.ErrorOccuredInMethod("", t.Exception);
                    throw t.Exception.InnerException;
                }
            });
        }

        internal Actor ActorImplementation => actorImplementation;
    }
}
