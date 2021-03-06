﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors.CodeGen;

namespace Stacks.Actors.Remote.CodeGen
{
    class ActorTypeBuilder
    {
        protected AssemblyBuilder asmBuilder;
        protected ModuleBuilder moduleBuilder;
        protected AssemblyName asmName;
        protected Dictionary<string, Type> messageParamTypes;
        protected Dictionary<string, Type> messageReturnTypes;
        protected Dictionary<string, Type> messageObsTypes;

        public ActorTypeBuilder(string assemblyName)
        {
            this.messageParamTypes = new Dictionary<string, Type>();
            this.messageReturnTypes = new Dictionary<string, Type>();
            this.messageObsTypes = new Dictionary<string, Type>();
        
            this.asmName = new AssemblyName(assemblyName);

#if DEBUG_CODEGEN
            this.asmBuilder = AppDomain.CurrentDomain
                                 .DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
#else
                  this.asmBuilder = AppDomain.CurrentDomain
                                       .DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
#endif

            this.moduleBuilder = asmBuilder.DefineDynamicModule(asmName + ".dll");
        }

        [Conditional("DEBUG_CODEGEN")]
        public void SaveToFile()
        {
            asmBuilder.Save(asmName.Name + ".dll");
        }

        public void DefineMessagesFromInterfaceType(Type actorInterface)
        {
            var methods = actorInterface.FindValidProxyMethods(onlyPublic: false);
            methods.EnsureNamesAreUnique();

            for (int i = 0; i < methods.Length; ++i)
            {
                DefineMessageTypeForActorMethodParams(methods[i]);
                DefineMessageTypeForActorMethodReturn(methods[i]); 
            }

            var properties = actorInterface.FindValidObservableProperties(onlyPublic: false);
            properties.EnsureNamesAreUnique();

            for (int i = 0; i < properties.Length; ++i)
            {
                var ii = properties[i].InterfaceInfo;
                DefineMessageTypeForActorObservable(ii.Name, ii.PropertyType);
            }

            var obsMethods = actorInterface.FindValidObservableMethods(onlyPublic: false);
            obsMethods.EnsureNamesAreUnique();

            for (int i = 0; i < obsMethods.Length; ++i)
            {
                var mi = obsMethods[i];
                DefineMessageTypeForActorObservable(mi.PublicName, mi.InterfaceInfo.ReturnType);
            }
        }

        private void DefineMessageTypeForActorMethodParams(MethodInfoMapping miMapping)
        {
            var messageTypeName = miMapping.PublicName + "Message";
            var typeBuilder = this.moduleBuilder.DefineType("Messages." + messageTypeName, TypeAttributes.Public);

            // Empty ctor
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.HideBySig);

            // Type attributes
            var protoContractCtor = typeof(ProtoBuf.ProtoContractAttribute).GetConstructor(Type.EmptyTypes);
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(protoContractCtor, new object[0]));

            // Properties
            var miParams = miMapping.Info.GetParameters()
                                               .ToArray();

            // Index used for proto member attribute. Must start with 1.
            for (int idx = 0, i = 1; idx < miParams.Length; ++idx)
            {
                var p = miParams[idx];

                // Ommit parameter in message if it is a IActorSession and is on first place
                if (p.ParameterType == typeof(IActorSession) && idx == 0)
                    continue;

                var fb = typeBuilder.DefineField(p.Name, p.ParameterType, FieldAttributes.Public);
                var protoMemberCtor = typeof(ProtoBuf.ProtoMemberAttribute).GetConstructor(new[] { typeof(int) });
                fb.SetCustomAttribute(new CustomAttributeBuilder(protoMemberCtor, new object[] { i }));
                ++i;
            }


            var createdType = typeBuilder.CreateType();
            this.messageParamTypes[miMapping.PublicName] = createdType;
        }

        private void DefineMessageTypeForActorMethodReturn(MethodInfoMapping miMapping)
        {
            Type returnType = null;

            var returnTypeTask = miMapping.Info.ReturnType;
            bool isEmptyReply = returnTypeTask == typeof(Task);
            if (returnTypeTask == typeof(Task))
                returnType = typeof(System.Reactive.Unit);
            else
            {
                var genArgs = returnTypeTask.GetGenericArguments();

                if (genArgs.Length != 1)
                    throw new InvalidOperationException("Return type of a method must be Task<T>");

                returnType = genArgs[0];
            }

            var messageTypeName = miMapping.PublicName + "MessageReply";
            var replyInterfaceType = typeof(IReplyMessage<>).MakeGenericType(returnType);
            var typeBuilder = this.moduleBuilder.DefineType("Messages." + messageTypeName, TypeAttributes.Public, null, new[] { replyInterfaceType });

            // Empty ctor
            var cb = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis, Type.EmptyTypes);
            var cbIl = cb.GetILGenerator();
            cbIl.Emit(OpCodes.Ret);

            // Type attributes
            var protoContractCtor = typeof(ProtoBuf.ProtoContractAttribute).GetConstructor(Type.EmptyTypes);
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(protoContractCtor, new object[0]));

            var protoMemberCtor = typeof(ProtoBuf.ProtoMemberAttribute).GetConstructor(new[] { typeof(int) });

            var errfb = typeBuilder.DefineField("$ErrorMessage", typeof(string), FieldAttributes.Public);
            errfb.SetCustomAttribute(new CustomAttributeBuilder(protoMemberCtor, new object[] { 1 }));

            FieldBuilder fb = null;

            if (!isEmptyReply)
            {
                fb = typeBuilder.DefineField("@Return", returnType, FieldAttributes.Public);
                fb.SetCustomAttribute(new CustomAttributeBuilder(protoMemberCtor, new object[] { 2 }));
            }

            //GetResult()
            var getResultMb = typeBuilder.DefineMethod("GetResult", MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig,
                CallingConventions.HasThis, returnType, Type.EmptyTypes);
            var gril = getResultMb.GetILGenerator();
            var isOKLabel = gril.DefineLabel();

            gril.Emit(OpCodes.Ldarg_0);
            gril.Emit(OpCodes.Ldfld, errfb);
            gril.Emit(OpCodes.Ldnull);
            gril.Emit(OpCodes.Ceq);
            //if($ErrorMessage != null) {
            gril.Emit(OpCodes.Brtrue_S, isOKLabel);

            // throw new Exception($ErrorMessage);
            gril.Emit(OpCodes.Ldarg_0);
            gril.Emit(OpCodes.Ldfld, errfb);
            gril.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] { typeof(string) }));
            gril.Emit(OpCodes.Throw);

            // } else {
            // return @Return; }
            gril.MarkLabel(isOKLabel);
            if (!isEmptyReply)
            {
                gril.Emit(OpCodes.Ldarg_0);
                gril.Emit(OpCodes.Ldfld, fb);
            }
            else
            {
                gril.EmitCall(OpCodes.Call, typeof(System.Reactive.Unit).GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetGetMethod(), null);
            }
            gril.Emit(OpCodes.Ret); 


            //SetResult
            var setResultMb = typeBuilder.DefineMethod("SetResult", MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig,
                CallingConventions.HasThis, typeof(void), new[] { returnType });
            var sril = setResultMb.GetILGenerator();

            if (!isEmptyReply)
            {
                sril.Emit(OpCodes.Ldarg_0);
                sril.Emit(OpCodes.Ldarg_1);
                sril.Emit(OpCodes.Stfld, fb);
            }
            sril.Emit(OpCodes.Ret); 

            //SetError
            var setErrorMb = typeBuilder.DefineMethod("SetError", MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig,
                CallingConventions.HasThis, typeof(void), new[] { typeof(string) });
            var seil = setErrorMb.GetILGenerator();

            seil.Emit(OpCodes.Ldarg_0);
            seil.Emit(OpCodes.Ldarg_1);
            seil.Emit(OpCodes.Stfld, errfb);
            seil.Emit(OpCodes.Ret); 


            var createdType = typeBuilder.CreateType();
            this.messageReturnTypes[miMapping.PublicName] = createdType;
        }
        
        protected FieldInfo GetFieldInfoFromProtobufMessage(Type t, int protoMessageIdx)
        {
            return t.GetFields()
                    .First(fi =>
                    {
                        var a = fi.GetCustomAttribute<ProtoBuf.ProtoMemberAttribute>();
                        if (a != null &&
                            a.Tag == protoMessageIdx)
                            return true;
                        return false;
                    });
        }


        private void DefineMessageTypeForActorObservable(string name, Type obsType)
        {
            var messageTypeName = name + "$ObsMessage";
            var typeBuilder = this.moduleBuilder.DefineType("Messages." + messageTypeName, TypeAttributes.Public);

            var protoMemberCtor = typeof(ProtoBuf.ProtoMemberAttribute).GetConstructor(new[] { typeof(int) });

            // Empty ctor
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.HideBySig);

            // Type attributes
            var protoContractCtor = typeof(ProtoBuf.ProtoContractAttribute).GetConstructor(Type.EmptyTypes);
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(protoContractCtor, new object[0]));

            var propTypeObs = obsType;
            var p = propTypeObs.GetGenericArguments()[0];

            var fb = typeBuilder.DefineField("$Value", p, FieldAttributes.Public);
            fb.SetCustomAttribute(new CustomAttributeBuilder(protoMemberCtor, new object[] { 1 }));

            var createdType = typeBuilder.CreateType();
            this.messageParamTypes[name] = createdType;
        }
    }
}
