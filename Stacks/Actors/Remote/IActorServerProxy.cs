using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IActorServerProxy
    {
        IPEndPoint BindEndPoint { get; }

        void Stop();
    }
}
