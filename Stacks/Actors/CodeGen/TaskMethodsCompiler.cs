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

        public void Implement(MethodInfo method, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            var mBuilder = wrapperBuilder.DefineMethod(method.Name,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                CallingConventions.HasThis, method.ReturnType,
                method.GetParameters().Select(p => p.ParameterType).ToArray());

            var il = mBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, typeof(ActorWrapperBase).GetField("actorImplementation", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Castclass, actorInterface);
            for (var i = 1; i <= method.GetParameters().Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, i);
            }

            il.EmitCall(OpCodes.Call, method, null);
            il.Emit(OpCodes.Ret);
        }

        public void Implement(PropertyInfo property, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            throw new NotSupportedException();
        }
    }
}
