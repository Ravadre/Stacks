using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Subjects;
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

            var baseCtor = typeof(ActorClientProxyTemplate).GetConstructor(new[] { typeof(IPEndPoint) });

            var ctorBuilder = actorImplBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                                    CallingConventions.HasThis, new[] { typeof(IPEndPoint) });
            var ctorIl = ctorBuilder.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldarg_1);
            ctorIl.Emit(OpCodes.Call, baseCtor);


            foreach (var method in actorInterface.FindValidProxyMethods())
            {
                var mb = actorImplBuilder.DefineMethod(method.Name, MethodAttributes.SpecialName |
                                                                    MethodAttributes.Public |
                                                                    MethodAttributes.Virtual |
                                                                    MethodAttributes.HideBySig |
                                                                    MethodAttributes.Final);
                mb.SetReturnType(method.ReturnType);
                mb.SetParameters(method.GetParameters().Select(p => p.ParameterType).ToArray());

                ImplementSendMethod(mb, method);
            }

            foreach (var property in actorInterface.FindValidObservableProperties())
            {
                var innerType = property.PropertyType.GetGenericArguments()[0];

                var fb = actorImplBuilder.DefineField(
                                            property.Name + "$Field",
                                            typeof(Subject<>).MakeGenericType(innerType),
                                            FieldAttributes.Private);

                var pb = actorImplBuilder.DefineProperty(property.Name,
                            PropertyAttributes.HasDefault, property.PropertyType, null);

                var getMethod = actorImplBuilder.DefineMethod("get_" + property.Name,
                                        MethodAttributes.Public |
                                        MethodAttributes.HideBySig |
                                        MethodAttributes.SpecialName |
                                        MethodAttributes.Virtual,
                                    property.PropertyType, Type.EmptyTypes);

                var il = getMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, fb);
                il.Emit(OpCodes.Ret);
                
                pb.SetGetMethod(getMethod);


                Type messageType = moduleBuilder.GetType("Messages." + property.Name + "$ObsMessage");
                var desMethod = typeof(IStacksSerializer).GetMethod("Deserialize").MakeGenericMethod(messageType);

                var handlerMethod = actorImplBuilder.DefineMethod(property.Name + "$ObsHandler",
                                        MethodAttributes.Private | MethodAttributes.HideBySig,
                                        CallingConventions.HasThis, typeof(void),
                                        new[] { typeof(MemoryStream) });

                var hil = handlerMethod.GetILGenerator();
                hil.Emit(OpCodes.Ldarg_0);
                hil.Emit(OpCodes.Ldfld, fb);
                hil.Emit(OpCodes.Ldarg_0);
                hil.Emit(OpCodes.Ldfld, typeof(ActorClientProxyTemplate).GetField("serializer", BindingFlags.Instance | BindingFlags.NonPublic));
                hil.Emit(OpCodes.Ldarg_1);
                hil.EmitCall(OpCodes.Callvirt, desMethod, null);
                hil.Emit(OpCodes.Ldfld, messageType.GetField("$Value"));
                hil.EmitCall(OpCodes.Call, typeof(Subject<>).MakeGenericType(innerType).GetMethod("OnNext"), null);
                hil.Emit(OpCodes.Ret);


      

                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Newobj, typeof(Subject<>).MakeGenericType(innerType).GetConstructor(Type.EmptyTypes));
                ctorIl.Emit(OpCodes.Stfld, fb);


                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Ldfld, typeof(ActorClientProxyTemplate).GetField("obsHandlers", BindingFlags.Instance | BindingFlags.NonPublic));
                ctorIl.Emit(OpCodes.Ldstr, property.Name);
                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Ldftn, handlerMethod);
                ctorIl.Emit(OpCodes.Newobj, typeof(Action<MemoryStream>).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                ctorIl.EmitCall(OpCodes.Call, typeof(Dictionary<string, Action<MemoryStream>>).GetMethod("set_Item"), null);
            }
            
            ctorIl.Emit(OpCodes.Ret);

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
