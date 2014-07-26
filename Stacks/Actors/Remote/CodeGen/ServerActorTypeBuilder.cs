using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Stacks.Tcp;

namespace Stacks.Actors.Remote.CodeGen
{
    public class ServerActorTypeBuilder : ActorTypeBuilder
    {
        private Type templateType;
        private Type actorType;
        private TypeBuilder actorImplBuilder;

        public ServerActorTypeBuilder(string assemblyName)
            : base(assemblyName)
        { }

        public Type CreateActorType(Type actorType)
        {
            templateType = typeof(ActorServerProxyTemplate<>).MakeGenericType(new[] { actorType });
            this.actorType = actorType;

            actorImplBuilder = moduleBuilder.DefineType("Impl$" + actorType.Name, TypeAttributes.Public,
                                        templateType, Type.EmptyTypes);

            {
                var baseCtor = templateType.GetConstructor(new[] { actorType, typeof(IPEndPoint) });

                var ctorBuilder = actorImplBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                                        CallingConventions.HasThis, new[] { actorType, typeof(IPEndPoint) });

                var il = ctorBuilder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call, baseCtor);

                foreach (var method in actorType.FindValidProxyMethods())
                {
                    var newMethod = CreateHandlerMethod(method);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, templateType.GetField("handlers", BindingFlags.Instance | BindingFlags.NonPublic));
                    il.Emit(OpCodes.Ldstr, method.Name);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldftn, newMethod);
                    il.Emit(OpCodes.Newobj, typeof(Action<FramedClient, long, MemoryStream>).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                    il.EmitCall(OpCodes.Call, typeof(Dictionary<string, Action<FramedClient, long, MemoryStream>>).GetMethod("set_Item"), null);
                }

                il.Emit(OpCodes.Ret);
            }

            ImplementHandleMessage();

            return actorImplBuilder.CreateType();
             
        }

        private MethodBuilder CreateHandlerMethod(MethodInfo method)
        {
            var sendMethodTaskRet = method.ReturnType;
            var sendMethodInnerRet = sendMethodTaskRet == typeof(Task)
                                        ? typeof(System.Reactive.Unit)
                                        : sendMethodTaskRet.GetGenericArguments()[0];

            var mb = actorImplBuilder.DefineMethod(method.Name + "Handler",
                                                 MethodAttributes.Private |
                                                 MethodAttributes.HideBySig,
                                               CallingConventions.HasThis,
                                               typeof(void),
                                               new[] { typeof(FramedClient), typeof(long), typeof(MemoryStream) });
            Type messageType = moduleBuilder.GetType("Messages." + method.Name + "Message");
            Type replyMessageType = moduleBuilder.GetType("Messages." + method.Name + "MessageReply");
            var desMethod = typeof(IStacksSerializer).GetMethod("Deserialize").MakeGenericMethod(messageType);

            var il = mb.GetILGenerator();

            var messageLocal = il.DeclareLocal(messageType);
            var endLabel = il.DefineLabel();

            // try {
            il.BeginExceptionBlock();

            //var msg = base.serializer.Deserialize<P>(ms);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, templateType.GetField("serializer", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ldarg_3);
            il.EmitCall(OpCodes.Callvirt, desMethod, null);
            il.Emit(OpCodes.Stloc_0);


            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);

            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, templateType.GetField("actorImplementation", BindingFlags.Instance | BindingFlags.NonPublic));

                //actorImplementation.[methodName](params);
                var sendParams = method.GetParameters();
                for (int i = 1; i <= sendParams.Length; ++i)
                {
                    var field = GetFieldInfoFromProtobufMessage(messageType, i);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldfld, field);
                }

                il.Emit(OpCodes.Call, method);
            }

            il.Emit(OpCodes.Newobj, replyMessageType.GetConstructor(Type.EmptyTypes));
            il.EmitCall(OpCodes.Call, templateType.GetMethod("HandleResponse", BindingFlags.Instance | BindingFlags.NonPublic)
                                                  .MakeGenericMethod(sendMethodInnerRet), null);

            // } catch (Exception e) {
            il.BeginCatchBlock(typeof(Exception));
            var excMsgLocal = il.DeclareLocal(typeof(string));
            var replyMsgLocal = il.DeclareLocal(typeof(IReplyMessage<>).MakeGenericType(sendMethodInnerRet));
            il.EmitCall(OpCodes.Call, typeof(Exception).GetProperty("Message").GetGetMethod(), null);
            il.Emit(OpCodes.Stloc, excMsgLocal);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Newobj, replyMessageType.GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Stloc, replyMsgLocal);
            il.Emit(OpCodes.Ldloc, replyMsgLocal);
            il.Emit(OpCodes.Ldloc, excMsgLocal);
            il.EmitCall(OpCodes.Callvirt, typeof(IReplyMessage<>).MakeGenericType(sendMethodInnerRet).GetMethod("SetError"), null);
            il.Emit(OpCodes.Ldloc, replyMsgLocal);
            il.EmitCall(OpCodes.Call, templateType.GetMethod("HandleResponse", BindingFlags.Instance | BindingFlags.NonPublic)
                                                .MakeGenericMethod(sendMethodInnerRet), null);

            // }
            il.EndExceptionBlock();

            il.MarkLabel(endLabel);
            il.Emit(OpCodes.Ret);

            return mb;
        }

        private void ImplementHandleMessage()
        {
            var method = actorImplBuilder.DefineMethod("HandleMessageAux", 
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
