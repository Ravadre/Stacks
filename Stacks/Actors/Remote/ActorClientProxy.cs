using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Tcp;

namespace Stacks.Actors
{
    public class ActorClientProxy
    {
        public static T Create<T>(IPEndPoint remoteEndPoint)
        {
            var type = typeof(T);
            return (T)Create(type, remoteEndPoint);
        }

        public static object Create(Type actorType, IPEndPoint remoteEndPoint)
        {
            var proxyCreator = new ActorClientProxy();

            return proxyCreator.AuxCreate(actorType, remoteEndPoint);
        }


        private Type actorType;
        private AssemblyBuilder asmBuilder;
        private ModuleBuilder moduleBuilder;
        private Dictionary<string, Type> messageParamTypes;
        private Dictionary<string, Type> messageReturnTypes;

        private object AuxCreate(Type actorType, IPEndPoint remoteEndPoint)
        {
            this.messageParamTypes = new Dictionary<string, Type>();
            this.messageReturnTypes = new Dictionary<string, Type>();
            this.actorType = actorType;

            Ensure.IsInterface(actorType, "actorType", "Only interfaces can be used to create actor client proxy");

            var methods = FindValidProxyMethods();
            EnsureMethodNamesAreUnique(methods);

            CreateModuleForActor();

            for (int i = 0; i < methods.Length; ++i)
            {
                DefineMessageTypeForActorMethodParams(methods[i]);
                DefineMessageTypeForActorMethodReturn(methods[i]);
            }

            CreateActorType(methods);

            asmBuilder.Save("ActorProxyModule_" + actorType.FullName + ".dll");

            return null;
        }

        private void EnsureMethodNamesAreUnique(IEnumerable<MethodInfo> methods)
        {
            var hs = new HashSet<string>();

            foreach (var m in methods)
            {
                if (!hs.Add(m.Name))
                    throw new InvalidOperationException("Method names must be unique when using " +
                        "an interface as a actor client proxy");
            }
        }

        private MethodInfo[] FindValidProxyMethods()
        {
            var t = actorType;
            return t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
                    .OrderBy(m => m.Name)
                    .ToArray();
        }

        private void CreateModuleForActor()
        {
            var t = actorType;
            var asmName = new AssemblyName("ActorProxyModule_" + t.FullName);

            this.asmBuilder = AppDomain.CurrentDomain
                                       .DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
            this.moduleBuilder = asmBuilder.DefineDynamicModule(asmName + ".dll");
        }

        private void DefineMessageTypeForActorMethodParams(MethodInfo methodInfo)
        {
            if (methodInfo.GetParameters().Length == 1)
            {
                var pType = methodInfo.GetParameters()[0].ParameterType;

                if (pType.GetCustomAttribute(typeof(ProtoBuf.ProtoContractAttribute)) != null)
                {
                    this.messageParamTypes[methodInfo.Name] = pType;
                    return;
                }
            }


            var messageTypeName = methodInfo.Name + "Message";
            var typeBuilder = this.moduleBuilder.DefineType("Messages." + messageTypeName, TypeAttributes.Public);

            // Empty ctor
            var cb = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis, Type.EmptyTypes);
            var cbIl = cb.GetILGenerator();
            cbIl.Emit(OpCodes.Ret);

            // Type attributes
            var protoContractCtor = typeof(ProtoBuf.ProtoContractAttribute).GetConstructor(Type.EmptyTypes);
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(protoContractCtor, new object[0]));

            // Properties
            var miParams = methodInfo.GetParameters()
                                     .OrderBy(p => p.Name)
                                     .ToArray();

            // Index used for proto member attribute. Must start with 1.
            for (int i = 1; i <= miParams.Length; ++i)
            {
                var p = miParams[i - 1];

                var fb = typeBuilder.DefineField(p.Name, p.ParameterType, FieldAttributes.Public);
                var protoMemberCtor = typeof(ProtoBuf.ProtoMemberAttribute).GetConstructor(new[] { typeof(int) });
                fb.SetCustomAttribute(new CustomAttributeBuilder(protoMemberCtor, new object[] { i }));
            }


            var createdType = typeBuilder.CreateType();
            this.messageParamTypes[methodInfo.Name] = createdType;
        }

        private void DefineMessageTypeForActorMethodReturn(MethodInfo methodInfo)
        {
            var returnTypeTask = methodInfo.ReturnType;
            if (returnTypeTask == typeof(Task))
                return;

            var genArgs = returnTypeTask.GetGenericArguments();

            if (genArgs.Length != 1)
                throw new InvalidOperationException("Return type of a method must be Task<T>");

            var returnType = genArgs[0];

            //if (returnType.GetCustomAttribute(typeof(ProtoBuf.ProtoContractAttribute)) != null)
            //{
            //    this.messageReturnTypes[methodInfo.Name] = returnType;
            //    return;
            //}

            var messageTypeName = methodInfo.Name + "MessageReply";
            var typeBuilder = this.moduleBuilder.DefineType("Messages." + messageTypeName, TypeAttributes.Public);

            // Empty ctor
            var cb = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis, Type.EmptyTypes);
            var cbIl = cb.GetILGenerator();
            cbIl.Emit(OpCodes.Ret);

            // Type attributes
            var protoContractCtor = typeof(ProtoBuf.ProtoContractAttribute).GetConstructor(Type.EmptyTypes);
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(protoContractCtor, new object[0]));

            var fb = typeBuilder.DefineField("@Return", returnType, FieldAttributes.Public);
            var protoMemberCtor = typeof(ProtoBuf.ProtoMemberAttribute).GetConstructor(new[] { typeof(int) });
            fb.SetCustomAttribute(new CustomAttributeBuilder(protoMemberCtor, new object[] { 1 }));

            var createdType = typeBuilder.CreateType();
            this.messageReturnTypes[methodInfo.Name] = createdType;
        }

        private void CreateActorType(MethodInfo[] methods)
        {
            var actorImplBuilder = moduleBuilder.DefineType("Impl$" + actorType.Name, TypeAttributes.Public,
                                        typeof(ActorClientProxyTemplate), new[] { actorType });

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

            foreach (var method in methods)
            {
                var mb = actorImplBuilder.DefineMethod(method.Name, MethodAttributes.SpecialName |
                                                                    MethodAttributes.Public |
                                                                    MethodAttributes.Virtual |
                                                                    MethodAttributes.HideBySig |
                                                                    MethodAttributes.Final);
                mb.SetReturnType(method.ReturnType);
                mb.SetParameters(method.GetParameters().Select(p => p.ParameterType).ToArray());

                var il = mb.GetILGenerator();
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            }

            var actorImplType = actorImplBuilder.CreateType();
        }
    }
}
