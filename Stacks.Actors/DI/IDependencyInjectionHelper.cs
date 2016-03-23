using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.DI
{
    public interface IDependencyInjectionHelper
    {
        T Resolve<T>();
        T Resolve<T>(IDictionary<string, object> args);
        T Resolve<T>(string resolveName, IDictionary<string, object> args = null);
        void Release<T>(T obj);
        void Register<I, TImpl>()
          where I : class
          where TImpl : Actor, I;

        void RegisterTransient<I, TImpl>()
          where I : class
          where TImpl : Actor, I;
    }
}
