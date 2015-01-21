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
using Stacks.Actors.Proto;

namespace Stacks.Actors.Remote.CodeGen
{
    class ClientActorTypeBuilder : ActorTypeBuilder
    {
        // Cache is vital because protobuf-net is not reclaiming serializers
        // therefore, when creating implementation for the same interface 
        // new serializers are being hold permanently.
        // For this reason, no eviction mechanism is implemented as well.
        private static Dictionary<Type, Type> constructedTypesCache = new Dictionary<Type, Type>();


        public ClientActorTypeBuilder(string assemblyName)
            : base(assemblyName)
        { }

        public Type CreateActorType(Type actorInterface)
        {
            Type implType = null;

//            lock (constructedTypesCache)
//            {
//                if (constructedTypesCache.TryGetValue(actorInterface, out implType))
//                    return implType;
//            }

            var proxyTemplateType = typeof(ActorClientProxyTemplate<>).MakeGenericType(actorInterface);
            var actorImplBuilder = moduleBuilder.DefineType("Impl$" + actorInterface.Name, TypeAttributes.Public,
                                        proxyTemplateType, new[] { actorInterface });

            var baseCtor = proxyTemplateType.GetConstructor(new[] { typeof(IPEndPoint), typeof(ActorClientProxyOptions) });

            var ctorBuilder = actorImplBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                                    CallingConventions.HasThis, new[] { typeof(IPEndPoint), typeof(ActorClientProxyOptions) });
            var ctorIl = ctorBuilder.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldarg_1);
            ctorIl.Emit(OpCodes.Ldarg_2);
            ctorIl.Emit(OpCodes.Call, baseCtor);

            {
                var actorProperty = actorImplBuilder.DefineProperty("Actor", PropertyAttributes.HasDefault, actorInterface, null);
                var actorGetMethodProperty = actorImplBuilder.DefineMethod("get_Actor",
                                                                MethodAttributes.Public |
                                                                MethodAttributes.HideBySig |
                                                                MethodAttributes.SpecialName |
                                                                MethodAttributes.Virtual,
                                                            actorInterface, Type.EmptyTypes);

                var il = actorGetMethodProperty.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ret);

                actorProperty.SetGetMethod(actorGetMethodProperty);
            }

            foreach (var method in actorInterface.FindValidProxyMethods(onlyPublic: true))
            {
                var mb = actorImplBuilder.DefineMethod(method.PublicName, MethodAttributes.SpecialName |
                                                                    MethodAttributes.Public |
                                                                    MethodAttributes.Virtual |
                                                                    MethodAttributes.HideBySig |
                                                                    MethodAttributes.Final);
                mb.SetReturnType(method.Info.ReturnType);
                mb.SetParameters(method.Info.GetParameters().Select(p => p.ParameterType).ToArray());

                ImplementSendMethod(mb, method.Info, proxyTemplateType);
            }

            foreach (var property in actorInterface.FindValidObservableProperties(onlyPublic: true))
            {
                var publicName = property.PublicName;
                var innerType = property.Info.PropertyType.GetGenericArguments()[0];

                var fb = actorImplBuilder.DefineField(
                                            property.PublicName + "$Field",
                                            typeof(Subject<>).MakeGenericType(innerType),
                                            FieldAttributes.Private);

                ImplementObservableProperty(actorImplBuilder, property.PublicName, property.Info.PropertyType, fb);
                var handlerMethod = ImplementObservableHandlerMethod(actorImplBuilder, publicName, fb, proxyTemplateType, innerType);
                ImplementObservableConstructorHandler(ctorIl, innerType, fb, proxyTemplateType, publicName, handlerMethod);
            }

            foreach (var method in actorInterface.FindValidObservableMethods(onlyPublic: true))
            {
                var publicName = method.PublicName;
                var innerType = method.Info.ReturnType.GetGenericArguments()[0];

                var fb = actorImplBuilder.DefineField(
                                         method.PublicName + "$Field",
                                         typeof(Subject<>).MakeGenericType(innerType),
                                         FieldAttributes.Private);

                ImplementObservableMethod(actorImplBuilder, method.PublicName, method.Info.ReturnType, fb);
                //ImplementObservableProperty(actorImplBuilder, method.PublicName, method.Info.ReturnType, fb);
                var handlerMethod = ImplementObservableHandlerMethod(actorImplBuilder, publicName, fb, proxyTemplateType, innerType);
                ImplementObservableConstructorHandler(ctorIl, innerType, fb, proxyTemplateType, publicName, handlerMethod);
            }

            ctorIl.Emit(OpCodes.Ret);

            implType = actorImplBuilder.CreateType();

            lock (constructedTypesCache)
            {
                constructedTypesCache[actorInterface] = implType;
            }
            return implType;
        }

        private void ImplementObservableConstructorHandler(ILGenerator ctorIl, Type innerType, FieldBuilder fb,
            Type proxyTemplateType, string publicName, MethodBuilder handlerMethod)
        {
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Newobj, typeof (Subject<>).MakeGenericType(innerType).GetConstructor(Type.EmptyTypes));
            ctorIl.Emit(OpCodes.Stfld, fb);


            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldfld, proxyTemplateType.GetField("obsHandlers", BindingFlags.Instance | BindingFlags.NonPublic));
            ctorIl.Emit(OpCodes.Ldstr, publicName);
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldftn, handlerMethod);
            ctorIl.Emit(OpCodes.Newobj, typeof (Action<ActorProtocolFlags, string, MemoryStream>).GetConstructor(new[] {typeof (object), typeof (IntPtr)}));
            ctorIl.EmitCall(OpCodes.Call, typeof(Dictionary<string, Action<ActorProtocolFlags, string, MemoryStream>>).GetMethod("set_Item"), null);
        }

        private MethodBuilder ImplementObservableHandlerMethod(TypeBuilder actorImplBuilder, string publicName,
            FieldBuilder fb, Type proxyTemplateType, Type innerType)
        {
            Type messageType = moduleBuilder.GetType("Messages." + publicName + "$ObsMessage");
            var handlerMethod = actorImplBuilder.DefineMethod(publicName + "$ObsHandler",
                MethodAttributes.Private | MethodAttributes.HideBySig,
                CallingConventions.HasThis, typeof (void),
                new[] {typeof(ActorProtocolFlags), typeof(string), typeof (MemoryStream)});

            var desMethod = typeof (ActorPacketSerializer).GetMethod("Deserialize").MakeGenericMethod(messageType);
            var hil = handlerMethod.GetILGenerator();
            hil.Emit(OpCodes.Ldarg_0);
            hil.Emit(OpCodes.Ldfld, fb);
            hil.Emit(OpCodes.Ldarg_0);
            hil.Emit(OpCodes.Ldfld, proxyTemplateType.GetField("serializer", BindingFlags.Instance | BindingFlags.NonPublic));
            hil.Emit(OpCodes.Ldarg_1);
            hil.Emit(OpCodes.Ldarg_2);
            hil.Emit(OpCodes.Ldarg_3);
            hil.EmitCall(OpCodes.Callvirt, desMethod, null);
            hil.Emit(OpCodes.Ldfld, messageType.GetField("$Value"));
            hil.EmitCall(OpCodes.Call, typeof (Subject<>).MakeGenericType(innerType).GetMethod("OnNext"), null);
            hil.Emit(OpCodes.Ret);
            return handlerMethod;
        }

        private static void ImplementObservableMethod(TypeBuilder actorImplBuilder, string publicName, Type type,
        FieldBuilder fb)
        {
            var getMethod = actorImplBuilder.DefineMethod(publicName,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.Virtual,
                type, Type.EmptyTypes);

            var il = getMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, fb);
            il.Emit(OpCodes.Ret);
        }

        private static void ImplementObservableProperty(TypeBuilder actorImplBuilder, string publicName, Type type,
            FieldBuilder fb)
        {
            var pb = actorImplBuilder.DefineProperty(publicName,
                PropertyAttributes.HasDefault, type, null);

            var getMethod = actorImplBuilder.DefineMethod("get_" + publicName,
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.Virtual,
                type, Type.EmptyTypes);

            var il = getMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, fb);
            il.Emit(OpCodes.Ret);

            pb.SetGetMethod(getMethod);
        }

        private void ImplementSendMethod(MethodBuilder mb, MethodInfo sendMethod, Type proxyTemplateType)
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

            var sendMessageTemplate = proxyTemplateType.GetMethod("SendMessage",
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
