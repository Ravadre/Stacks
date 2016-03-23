using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    [Serializable]
    public class ActorStoppedException : Exception
    {
        public ActorStoppedException()
        {
        }

        public ActorStoppedException(string message) : base(message)
        {
        }

        public ActorStoppedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ActorStoppedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
