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
        void Implement(MethodInfo method, TypeBuilder wrapperBuilder);
        void Implement(PropertyInfo property, TypeBuilder wrapperBuilder);
    }
}
