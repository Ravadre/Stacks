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
        private Dictionary<string, Type> messageTypes;

        private object AuxCreate(Type actorType, IPEndPoint remoteEndPoint)
        {
            this.messageTypes = new Dictionary<string,Type>();
            this.actorType = actorType;

            Ensure.IsInterface(actorType, "actorType", "Only interfaces can be used to create actor client proxy");

            var methods = FindValidProxyMethods();
            EnsureMethodNamesAreUnique(methods);

            Console.WriteLine("Found methods for actor client proxy:");
            foreach (var m in methods)
                Console.WriteLine(m.Name);

            CreateModuleForActor();

            for (int i = 0; i < methods.Length; ++i)
            {
                DefineMessageTypeForActorMethod(methods[i], i);
            }

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

        private void DefineMessageTypeForActorMethod(MethodInfo methodInfo, int idx)
        {
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
            this.messageTypes[messageTypeName] = createdType;
        }
    }
}
