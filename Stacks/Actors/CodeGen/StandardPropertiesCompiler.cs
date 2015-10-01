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

        public void Implement(PropertyInfoMapping property, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            var prop = wrapperBuilder.DefineProperty(property.PublicName, PropertyAttributes.None, CallingConventions.HasThis,
               property.InterfaceInfo.PropertyType, null);

            {
                var getMethod = wrapperBuilder.DefineMethod("get_" + property.PublicName,
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName |
                    MethodAttributes.Virtual,
                    property.InterfaceInfo.PropertyType, Type.EmptyTypes);

                var il = getMethod.GetILGenerator();
                il.DeclareLocal(typeof(Exception));

                il.BeginExceptionBlock();
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld,
                        typeof (ActorWrapperBase).GetField("actorImplementation",
                            BindingFlags.Instance | BindingFlags.NonPublic));
                    il.Emit(OpCodes.Castclass, actorInterface);

                    il.EmitCall(OpCodes.Callvirt, property.InterfaceInfo.GetGetMethod(true), null);
                    il.Emit(OpCodes.Ret);
                }
                // } catch (Exception) {
                il.BeginCatchBlock(typeof(Exception));
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

                prop.SetGetMethod(getMethod);
            }

            {
                var setMethod = wrapperBuilder.DefineMethod("set_" + property.PublicName,
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName |
                    MethodAttributes.Virtual, typeof (void), new[] {property.Info.PropertyType});

                var il = setMethod.GetILGenerator();
                il.DeclareLocal(typeof(Exception));

                il.BeginExceptionBlock();
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld,
                        typeof(ActorWrapperBase).GetField("actorImplementation",
                            BindingFlags.Instance | BindingFlags.NonPublic));
                    il.Emit(OpCodes.Castclass, actorInterface);
                    il.Emit(OpCodes.Ldarg, 1);
                    il.EmitCall(OpCodes.Callvirt, property.InterfaceInfo.GetSetMethod(true), null);
                    il.Emit(OpCodes.Ret);
                }
                // } catch (Exception) {
                il.BeginCatchBlock(typeof(Exception));
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
