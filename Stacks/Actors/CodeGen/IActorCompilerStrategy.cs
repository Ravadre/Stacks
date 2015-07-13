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
        bool CanCompile(MethodInfo method);
        bool CanCompile(PropertyInfo property);
        void Implement(MethodInfo method, Type actorInterface, TypeBuilder wrapperBuilder);
        void Implement(PropertyInfo property, Type actorInterface, TypeBuilder wrapperBuilder);
    }
}
