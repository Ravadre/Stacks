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
            return property.InterfaceInfo.PropertyType.GetGenericTypeDefinition() == typeof (IObservable<>);
        }

        public void Implement(MethodInfoMapping method, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            throw new NotSupportedException();
        }

        public void Implement(PropertyInfoMapping property, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            var prop = wrapperBuilder.DefineProperty(property.PublicName, PropertyAttributes.None, CallingConventions.HasThis,
                property.InterfaceInfo.PropertyType, null);

            var getMethod = wrapperBuilder.DefineMethod("get_" + property.PublicName,
               MethodAttributes.Public |
               MethodAttributes.HideBySig |
               MethodAttributes.SpecialName |
               MethodAttributes.Virtual,
               property.InterfaceInfo.PropertyType, Type.EmptyTypes);

            var il = getMethod.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, typeof(ActorWrapperBase).GetField("actorImplementation", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Castclass, actorInterface);

            il.EmitCall(OpCodes.Callvirt, property.InterfaceInfo.GetGetMethod(true), null);
            il.Emit(OpCodes.Ret);

            prop.SetGetMethod(getMethod);
        }
    }
}
