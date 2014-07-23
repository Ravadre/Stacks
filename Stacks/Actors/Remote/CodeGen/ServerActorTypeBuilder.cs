using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.Remote.CodeGen
{
    public class ServerActorTypeBuilder : ActorTypeBuilder
    {
        public ServerActorTypeBuilder(string assemblyName)
            : base(assemblyName)
        { }

        public Type CreateActorType(Type actorType)
        {
            var actorImplBuilder = moduleBuilder.DefineType("Impl$" + actorType.Name, TypeAttributes.Public,
                                        typeof(ActorServerProxyTemplate), Type.EmptyTypes);

            {
                var baseCtor = typeof(ActorServerProxyTemplate).GetConstructor(new[] { typeof(object), typeof(IPEndPoint) });

                var ctorBuilder = actorImplBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                                        CallingConventions.HasThis, new[] { typeof(object), typeof(IPEndPoint) });
                var il = ctorBuilder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call, baseCtor);

                il.Emit(OpCodes.Ret);
            }

            ImplementHandleMessage(actorImplBuilder);

            return actorImplBuilder.CreateType();
        }

        private void ImplementHandleMessage(TypeBuilder actorBuilder)
        {
            var method = actorBuilder.DefineMethod("HandleMessageAux", 
                                                        MethodAttributes.Public | 
                                                        MethodAttributes.HideBySig | 
                                                        MethodAttributes.Virtual, 
                                                    CallingConventions.HasThis, 
                                                    typeof(void), 
                                                    new[] { typeof(string), typeof(MemoryStream) });

            var il = method.GetILGenerator();

            il.Emit(OpCodes.Ret);
        }
    }
}
