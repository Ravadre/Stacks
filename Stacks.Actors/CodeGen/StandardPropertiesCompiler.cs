using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class StandardPropertiesCompiler : IActorCompilerStrategy
    {
        public bool CanCompile(MethodInfoMapping method)
        {
            return false;
        }

        public bool CanCompile(PropertyInfoMapping property)
        {
            var propType = property.InterfaceInfo.PropertyType;
            return (propType.IsGenericType && propType.GetGenericTypeDefinition() != typeof(IObservable<>)) ||
                    !propType.IsGenericType;
        }

        public void Implement(MethodInfoMapping method, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            throw new NotSupportedException();
        }

        private void ImplementGetMethod(ILGenerator il, Type actorInterface, MethodInfo getMethod)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld,
                typeof(ActorWrapperBase).GetField("actorImplementation",
                    BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Castclass, actorInterface);

            il.EmitCall(OpCodes.Callvirt, getMethod, null);
            il.Emit(OpCodes.Ret);
        }

        private void ImplementSetMethod(ILGenerator il, Type actorInterface, MethodInfo setMethod)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld,
                typeof(ActorWrapperBase).GetField("actorImplementation",
                    BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Castclass, actorInterface);
            il.Emit(OpCodes.Ldarg, 1);
            il.EmitCall(OpCodes.Callvirt, setMethod, null);
            il.Emit(OpCodes.Ret);
        }

        public void Implement(PropertyInfoMapping property, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            var prop = wrapperBuilder.DefineProperty(property.PublicName, PropertyAttributes.None, CallingConventions.HasThis,
               property.InterfaceInfo.PropertyType, null);

            var excHandling = property.InterfaceInfo.CustomAttributes.All(a => a.AttributeType != typeof(NoExceptionHandlerAttribute));

            if (property.InterfaceInfo.GetGetMethod(true) != null)
            {
                var getMethod = wrapperBuilder.DefineMethod("get_" + property.PublicName,
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName |
                    MethodAttributes.Virtual,
                    property.InterfaceInfo.PropertyType, Type.EmptyTypes);

                var il = getMethod.GetILGenerator();

                if (excHandling)
                {
                    il.DeclareLocal(typeof (Exception));
                    il.BeginExceptionBlock();
                }
                ImplementGetMethod(il, actorInterface, property.InterfaceInfo.GetGetMethod(true));
                
                if (excHandling)
                {
                    il.BeginCatchBlock(typeof (Exception));
                    {
                        il.Emit(OpCodes.Stloc_0);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, property.PublicName);
                        il.Emit(OpCodes.Ldloc_0);
                        il.EmitCall(OpCodes.Call, GetHelperMethod(), null);
                        il.Emit(OpCodes.Rethrow);
                    }
                    il.EndExceptionBlock();
                    il.Emit(OpCodes.Ret);
                }

                prop.SetGetMethod(getMethod);
            }

            if (property.InterfaceInfo.GetSetMethod(true) != null)
            {
                var setMethod = wrapperBuilder.DefineMethod("set_" + property.PublicName,
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName |
                    MethodAttributes.Virtual, typeof (void), new[] {property.Info.PropertyType});

                var il = setMethod.GetILGenerator();

                if (excHandling)
                {
                    il.DeclareLocal(typeof (Exception));
                    il.BeginExceptionBlock();
                }

                ImplementSetMethod(il, actorInterface, property.InterfaceInfo.GetSetMethod(true));

                if (excHandling)
                {
                    il.BeginCatchBlock(typeof (Exception));
                    {
                        il.Emit(OpCodes.Stloc_0);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, property.PublicName);
                        il.Emit(OpCodes.Ldloc_0);
                        il.EmitCall(OpCodes.Call, GetHelperMethod(), null);
                        il.Emit(OpCodes.Rethrow);
                    }
                    il.EndExceptionBlock();
                    il.Emit(OpCodes.Ret);
                }

                prop.SetSetMethod(setMethod);
            }
        }

        private MethodInfo GetHelperMethod()
        {
            return typeof(ActorWrapperBase)
                        .GetMethod("StopActorAndNotifySystem", BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
