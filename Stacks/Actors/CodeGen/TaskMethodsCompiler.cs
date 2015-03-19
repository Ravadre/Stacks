using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class TaskMethodsCompiler : IActorCompilerStrategy
    {
        public bool CanCompile(MethodInfo method)
        {
            var retType = method.ReturnType;
            return typeof (Task).IsAssignableFrom(retType) ||
                   typeof (Task<>).IsAssignableFrom(retType);
        }

        public bool CanCompile(PropertyInfo property)
        {
            return false;
        }

        public void Implement(MethodInfo method, TypeBuilder wrapperBuilder)
        {

        }

        public void Implement(PropertyInfo property, TypeBuilder wrapperBuilder)
        {
            throw new NotSupportedException();
        }
    }
}
