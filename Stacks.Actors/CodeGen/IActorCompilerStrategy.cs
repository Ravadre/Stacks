using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public interface IActorCompilerStrategy
    {
        bool CanCompile(MethodInfoMapping method);
        bool CanCompile(PropertyInfoMapping property);
        void Implement(MethodInfoMapping method, Type actorInterface, TypeBuilder wrapperBuilder);
        void Implement(PropertyInfoMapping property, Type actorInterface, TypeBuilder wrapperBuilder);
    }
}
