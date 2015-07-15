using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class ObservablePropertiesCompiler : IActorCompilerStrategy
    {
        public bool CanCompile(MethodInfoMapping method)
        {
            return false;
        }

        public bool CanCompile(PropertyInfoMapping property)
        {
            return property.InterfaceInfo.PropertyType == typeof (IObservable<>);
        }

        public void Implement(MethodInfoMapping method, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            throw new NotSupportedException();
        }

        public void Implement(PropertyInfoMapping property, Type actorInterface, TypeBuilder wrapperBuilder)
        {
//            var mBuilder = wrapperBuilder.DefineProperty(method.Name,
//               MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
//               CallingConventions.HasThis, method.ReturnType,
//               method.GetParameters().Select(p => p.ParameterType).ToArray());
//            var mParams = method.GetParameters();
//
//            for (var i = 1; i <= mParams.Length; ++i)
//            {
//                mBuilder.DefineParameter(i, ParameterAttributes.None, mParams[i - 1].Name);
//            }
//
//            var il = mBuilder.GetILGenerator();
//
//            il.Emit(OpCodes.Ldarg_0);
//            il.Emit(OpCodes.Ldfld, typeof(ActorWrapperBase).GetField("actorImplementation", BindingFlags.Instance | BindingFlags.NonPublic));
//            il.Emit(OpCodes.Castclass, actorInterface);
//            for (var i = 1; i <= method.GetParameters().Length; ++i)
//            {
//                il.Emit(OpCodes.Ldarg, i);
//            }
//
//            il.EmitCall(OpCodes.Call, method, null);
//            il.Emit(OpCodes.Ret);
        }
    }
}
