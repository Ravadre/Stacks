using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.Remote.CodeGen
{
    public class ClientActorTypeBuilder : ActorTypeBuilder
    {

        public ClientActorTypeBuilder(string assemblyName)
            : base(assemblyName)
        { }

        public Type CreateActorType(Type actorInterface)
        {
            var actorImplBuilder = moduleBuilder.DefineType("Impl$" + actorInterface.Name, TypeAttributes.Public,
                                        typeof(ActorClientProxyTemplate), new[] { actorInterface });

            {
                var baseCtor = typeof(ActorClientProxyTemplate).GetConstructor(new[] { typeof(IPEndPoint) });

                var ctorBuilder = actorImplBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                                        CallingConventions.HasThis, new[] { typeof(IPEndPoint) });
                var il = ctorBuilder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, baseCtor);

                il.Emit(OpCodes.Ret);
            }

            foreach (var method in actorInterface.FindValidProxyMethods())
            {
                var mb = actorImplBuilder.DefineMethod(method.Name, MethodAttributes.SpecialName |
                                                                    MethodAttributes.Public |
                                                                    MethodAttributes.Virtual |
                                                                    MethodAttributes.HideBySig |
                                                                    MethodAttributes.Final);
                mb.SetReturnType(method.ReturnType);
                mb.SetParameters(method.GetParameters().OrderBy(p => p.Name).Select(p => p.ParameterType).ToArray());

                ImplementSendMethod(mb, method);
            }

            return actorImplBuilder.CreateType();
        }

        private void ImplementSendMethod(MethodBuilder mb, MethodInfo sendMethod)
        {
            var il = mb.GetILGenerator();
            var msgType = messageParamTypes[sendMethod.Name];
            var msgReplyType = messageReturnTypes[sendMethod.Name];
            var msgCtor = msgType.GetConstructor(Type.EmptyTypes);


            //params packing
            var msgLocal = il.DeclareLocal(msgType);
            var sendParams = sendMethod.GetParameters();

            il.Emit(OpCodes.Newobj, msgCtor);
            il.Emit(OpCodes.Stloc_0);

            for (int i = 1; i <= sendParams.Length; ++i)
            {
                var field = GetFieldInfoFromProtobufMessage(msgType, i);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldarg, i);
                il.Emit(OpCodes.Stfld, field);
            }

            //call base.SendMessage
            var sendMethodTaskRet = sendMethod.ReturnType;
            var sendMethodInnerRet = sendMethodTaskRet == typeof(Task)
                                        ? typeof(System.Reactive.Unit)
                                        : sendMethodTaskRet.GetGenericArguments()[0];

            var sendMessageTemplate = typeof(ActorClientProxyTemplate).GetMethod("SendMessage",
                                                                         BindingFlags.Instance | BindingFlags.NonPublic);
            var sendMessage = sendMessageTemplate.MakeGenericMethod(msgType, sendMethodInnerRet, msgReplyType);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, sendMethod.Name);
            il.Emit(OpCodes.Ldloc_0);
            il.EmitCall(OpCodes.Call, sendMessage, null);
            il.Emit(OpCodes.Ret);
        }
    }
}
