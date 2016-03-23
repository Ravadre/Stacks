using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class StandardMethodCompiler : IActorCompilerStrategy
    {
        public bool CanCompile(MethodInfoMapping method)
        {
            var retType = method.InterfaceInfo.ReturnType;
            return !(typeof(Task).IsAssignableFrom(retType) &&
                     typeof(Task<>).IsAssignableFrom(retType) &&
                     (retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(IObservable<>)));
        }

        public bool CanCompile(PropertyInfoMapping property)
        {
            return false;
        }

        private void ImplementMethodCall(ILGenerator il, Type actorInterface, MethodInfo mi)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld,
                typeof(ActorWrapperBase).GetField("actorImplementation",
                    BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Castclass, actorInterface);
            for (var i = 1; i <= mi.GetParameters().Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, i);
            }

            il.EmitCall(OpCodes.Call, mi, null);
            il.Emit(OpCodes.Ret);
        }

        public void Implement(MethodInfoMapping method, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            var mi = method.InterfaceInfo;

            var excHandling = mi.CustomAttributes.All(a => a.AttributeType != typeof (NoExceptionHandlerAttribute));

            var mBuilder = wrapperBuilder.DefineMethod(method.PublicName,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                CallingConventions.HasThis, mi.ReturnType,
                mi.GetParameters().Select(p => p.ParameterType).ToArray());
            var mParams = mi.GetParameters();

            for (var i = 1; i <= mParams.Length; ++i)
            {
                mBuilder.DefineParameter(i, ParameterAttributes.None, mParams[i - 1].Name);
            }

            var il = mBuilder.GetILGenerator();
            if (excHandling)
            {
                il.DeclareLocal(typeof (Exception));
                il.BeginExceptionBlock();
            }
            // try {
            // ((actorInterface)base.actorImplementation).method(params...)
            {
                ImplementMethodCall(il, actorInterface, mi);
            }
            // } catch (Exception) {
            if (excHandling)
            {
                il.BeginCatchBlock(typeof (Exception));
                {
                    il.Emit(OpCodes.Stloc_0);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, method.PublicName);
                    il.Emit(OpCodes.Ldloc_0);
                    il.EmitCall(OpCodes.Call, GetHelperMethod(), null);
                    il.Emit(OpCodes.Rethrow);
                }
                il.EndExceptionBlock();
                il.Emit(OpCodes.Ret);
            }
        }

        public void Implement(PropertyInfoMapping property, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            throw new NotSupportedException();
        }

        private MethodInfo GetHelperMethod()
        {
            return typeof(ActorWrapperBase)
                        .GetMethod("StopActorAndNotifySystem", BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
