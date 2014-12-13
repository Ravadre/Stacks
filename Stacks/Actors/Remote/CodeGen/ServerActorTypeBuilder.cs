﻿using System;
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
    class ServerActorTypeBuilder : ActorTypeBuilder
    {
        // Cache is vital because protobuf-net is not reclaiming serializers
        // therefore, when creating implementation for the same interface 
        // new serializers are being hold permanently.
        // For this reason, no eviction mechanism is implemented as well.
        private static Dictionary<Type, Type> constructedTypesCache = new Dictionary<Type, Type>();


        private Type templateType;
        private Type actorType;
        private TypeBuilder actorImplBuilder;

        public ServerActorTypeBuilder(string assemblyName)
            : base(assemblyName)
        { }

        public Type CreateActorType(Type actorType)
        {
            Type implType = null;
            lock (constructedTypesCache)
            {
                if (constructedTypesCache.TryGetValue(actorType, out implType))
                    return implType;
            }

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

                foreach (var miMapping in actorType.FindValidProxyMethods(onlyPublic: false))
                {
                    var newMethod = CreateHandlerMethod(miMapping);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, templateType.GetField("handlers", BindingFlags.Instance | BindingFlags.NonPublic));
                    il.Emit(OpCodes.Ldstr, miMapping.PublicName);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldftn, newMethod);
                    il.Emit(OpCodes.Newobj, typeof(Action<FramedClient, long, MemoryStream>).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                    il.EmitCall(OpCodes.Call, typeof(Dictionary<string, Action<FramedClient, long, MemoryStream>>).GetMethod("set_Item"), null);
                }

                foreach (var property in actorType.FindValidObservableProperties(onlyPublic: false))
                {
                    var newMethod = CreatePropertyHandler(property.InterfaceInfo);
                    var errMethod = CreateErrorHandler(property.InterfaceInfo);

                    var innerPropType = property.InterfaceInfo.PropertyType.GetGenericArguments()[0];
                    var actionType = typeof(Action<>).MakeGenericType(new[] { innerPropType });
                    var errorActionType = typeof(Action<>).MakeGenericType(new[] { typeof(Exception) });

                    var obsMethod = typeof(ObservableExtensions)
                                        .GetMethods(BindingFlags.Static | BindingFlags.Public)
                                        .Where(m => m.Name == "Subscribe")
                                        .Select(m => new
                                        {
                                            Method = m,
                                            Params = m.GetParameters(),
                                            Args = m.GetGenericArguments()
                                        })
                                        .Where(x =>
                                        {
                                            var m = x.Method;
                                            var p = x.Params;
                                            if (m.IsGenericMethod && p.Length == 3)
                                            {
                                                if (p[0].ParameterType.GetGenericTypeDefinition() == typeof(IObservable<>) &&
                                                    p[1].ParameterType.GetGenericTypeDefinition() == typeof(Action<>) &&
                                                    p[2].ParameterType.GetGenericTypeDefinition() == typeof(Action<>))
                                                    return true;
                                            }
                                            return false;
                                        })
                                        .Select(x => x.Method)
                                        .First()
                                        .MakeGenericMethod(innerPropType);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, templateType.GetField("actorImplementation", BindingFlags.Instance | BindingFlags.NonPublic));
                    il.EmitCall(OpCodes.Callvirt, property.InterfaceInfo.GetGetMethod(true), null);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldftn, newMethod);
                    il.Emit(OpCodes.Newobj, actionType.GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldftn, errMethod);
                    il.Emit(OpCodes.Newobj, errorActionType.GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                    il.EmitCall(OpCodes.Call, obsMethod, null);
                    il.Emit(OpCodes.Pop);
                }

                il.Emit(OpCodes.Ret);
            }

            implType = actorImplBuilder.CreateType();

            lock (constructedTypesCache)
            {
                constructedTypesCache[actorType] = implType;
            }

            return implType;
        }

        private MethodBuilder CreateErrorHandler(PropertyInfo property)
        {
            var mb = actorImplBuilder.DefineMethod(property.Name + "$ObservableErrorHandler",
                                                       MethodAttributes.Private |
                                                       MethodAttributes.HideBySig,
                                                   CallingConventions.HasThis,
                                                   typeof(void),
                                                   new[] { typeof(Exception) });
            var il = mb.GetILGenerator();
            il.Emit(OpCodes.Ret);

            return mb;
        }

        private MethodBuilder CreatePropertyHandler(PropertyInfo property)
        {
            var propType = property.PropertyType;
            var innerType = propType.GetGenericArguments()[0];

            var mb = actorImplBuilder.DefineMethod(property.Name + "$ObservableHandler",
                                                       MethodAttributes.Private |
                                                       MethodAttributes.HideBySig,
                                                   CallingConventions.HasThis,
                                                   typeof(void),
                                                   new[] { innerType });

            var msgType = moduleBuilder.GetType("Messages." + property.Name + "$ObsMessage");
            var sendMessageMethod = templateType.GetMethod("SendObs", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(msgType);

            var il = mb.GetILGenerator();

            il.DeclareLocal(msgType);

            il.Emit(OpCodes.Newobj, msgType.GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, msgType.GetField("$Value"));

            il.Emit(OpCodes.Ldarg_0);
            //il.Emit(OpCodes.Ldfld, templateType.GetField("actorImplementation", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ldstr, property.Name);
            il.Emit(OpCodes.Ldloc_0);
            il.EmitCall(OpCodes.Call, sendMessageMethod, null);

            il.Emit(OpCodes.Ret);

            return mb;
        }

        private MethodBuilder CreateHandlerMethod(MethodInfoMapping miMapping)
        {
            var sendMethodTaskRet = miMapping.Info.ReturnType;
            bool isNoParamTask = sendMethodTaskRet == typeof(Task);
            var sendMethodInnerRet = sendMethodTaskRet == typeof(Task)
                                        ? typeof(System.Reactive.Unit)
                                        : sendMethodTaskRet.GetGenericArguments()[0];

            var mb = actorImplBuilder.DefineMethod(miMapping.PublicName + "Handler",
                                                 MethodAttributes.Private |
                                                 MethodAttributes.HideBySig,
                                               CallingConventions.HasThis,
                                               typeof(void),
                                               new[] { typeof(FramedClient), typeof(long), typeof(MemoryStream) });
            Type messageType = moduleBuilder.GetType("Messages." + miMapping.PublicName + "Message");
            Type replyMessageType = moduleBuilder.GetType("Messages." + miMapping.PublicName + "MessageReply");
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

                var sendParams = miMapping.Info.GetParameters();

                // If first parameter is IActorSession, load session for dictionary and 
                // place it on stack as first parameter.
                if (sendParams.Length >= 1 &&
                    sendParams[0].ParameterType == typeof(IActorSession))
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, templateType.GetField("actorSessions", BindingFlags.Instance | BindingFlags.NonPublic));
                    il.Emit(OpCodes.Ldarg_1); //FramedClient used as a key for session cache
                    il.EmitCall(OpCodes.Call, typeof(Dictionary<IFramedClient, IActorSession>).GetMethod("get_Item"), null);
                }

                for (int idx = 0, i = 1; idx < sendParams.Length; ++idx)
                {
                    // Ommit first parameter if it is an IActorSession
                    if (idx == 0 && sendParams[0].ParameterType == typeof(IActorSession))
                        continue;

                    var field = GetFieldInfoFromProtobufMessage(messageType, i);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldfld, field);

                    ++i;
                }

                il.EmitCall(OpCodes.Callvirt, miMapping.InterfaceInfo, null);
            }

            il.Emit(OpCodes.Newobj, replyMessageType.GetConstructor(Type.EmptyTypes));

            if (isNoParamTask)
            {
                il.EmitCall(OpCodes.Call, templateType.GetMethod("HandleResponseNoResult", BindingFlags.Instance | BindingFlags.NonPublic), null);
            }
            else
            {
                il.EmitCall(OpCodes.Call, templateType.GetMethod("HandleResponse", BindingFlags.Instance | BindingFlags.NonPublic)
                                                      .MakeGenericMethod(sendMethodInnerRet),
                                          null);
            }

            // } catch (Exception e) {
            il.BeginCatchBlock(typeof(Exception));
            var excMsgLocal = il.DeclareLocal(typeof(string));
            var replyMsgLocal = il.DeclareLocal(typeof(IReplyMessage<>).MakeGenericType(sendMethodInnerRet));

            //var excMsgLocal = e.Message;
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

            if (isNoParamTask)
            {
                il.EmitCall(OpCodes.Call, templateType.GetMethod("HandleResponseNoResult", BindingFlags.Instance | BindingFlags.NonPublic), null);
            }
            else
            {
                il.EmitCall(OpCodes.Call, templateType.GetMethod("HandleResponse", BindingFlags.Instance | BindingFlags.NonPublic)
                                                      .MakeGenericMethod(sendMethodInnerRet),
                                          null);
            }


            // }
            il.EndExceptionBlock();

            il.MarkLabel(endLabel);
            il.Emit(OpCodes.Ret);

            return mb;
        }
    }
}
