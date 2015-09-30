using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.DI
{
    public interface IDependencyResolver
    {
        T Resolve<T>(string resolverKey, IDictionary arguments);
        void Release(IActor actor);

    }
}
