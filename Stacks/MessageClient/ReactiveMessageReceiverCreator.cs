using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    class ReactiveMessageReceiverCreator<T>
    {
        private Dictionary<string, object> subjectsToInject;
        private TypeBuilder tb;

        private readonly IMessageIdCache messageIdCache;
        private readonly IStacksSerializer packetSerializer;

        public ReactiveMessageReceiverCreator(IMessageIdCache messageIdCache,
            IStacksSerializer packetSerializer)
        {
            this.messageIdCache = messageIdCache;
            this.packetSerializer = packetSerializer;
        }

        public T CreateReceiverImplementation(out Dictionary<int, Action<MemoryStream>> deserializeByMessageId)
        {
            tb = CreateTypeBuilder();
            subjectsToInject = new Dictionary<string, object>();

            deserializeByMessageId = new Dictionary<int, Action<MemoryStream>>();

            EnsureTypeIsValid();

            foreach (var property in GetTypeObservableProperties())
            {
                ParseProperty(property, deserializeByMessageId);
            }

            var myType = tb.CreateType();

            var packetsImplementation = (T)Activator.CreateInstance(myType);

            InjectsSubjectsToImpl(myType, packetsImplementation);

            return packetsImplementation;
        }

        private TypeBuilder CreateTypeBuilder()
        {
            var asmName = new AssemblyName("MessageClient_" + typeof(T).Name + Guid.NewGuid().ToString());
            var dynAsm = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);

            var typeB = dynAsm.DefineDynamicModule("MessageClient_" + typeof(T).Name + ".dll")
                              .DefineType(typeof(T).Name + "Impl", TypeAttributes.Public,
                                          null, new[] { typeof(T) });

            return typeB;
        }

        private void ParseProperty(PropertyInfo property, Dictionary<int, Action<MemoryStream>> deserializeByMessageId)
        {
            var packetType = property.PropertyType.GetGenericArguments().First();
            int messageId = this.messageIdCache.GetMessageId(packetType);

            var subjectType = typeof(Subject<>).MakeGenericType(packetType);
            var subject = Activator.CreateInstance(subjectType);

            var pName = property.Name;
            var fieldName = pName + "field";

            subjectsToInject[fieldName] = subject;

            deserializeByMessageId[messageId] = (Action<MemoryStream>)
                typeof(ReactiveMessageReceiverCreator<T>)
                    .GetMethod("SetupDeserializeAction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .MakeGenericMethod(packetType)
                    .Invoke(this, new[] { subject });


            var fb = tb.DefineField(fieldName, subjectType, FieldAttributes.Private);
            var pb = tb.DefineProperty(pName, PropertyAttributes.HasDefault,
                                          property.PropertyType, null);
            var mb = tb.DefineMethod("get_" + pName,
                                        MethodAttributes.Public |
                                            MethodAttributes.SpecialName |
                                            MethodAttributes.HideBySig |
                                            MethodAttributes.Virtual,
                                        property.PropertyType,
                                        Type.EmptyTypes);
            var ilGen = mb.GetILGenerator();

            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, fb);

            ilGen.Emit(OpCodes.Call, typeof(Observable).GetMethod("AsObservable",
                                                                  BindingFlags.Public |
                                                                    BindingFlags.Static)
                                                       .MakeGenericMethod(packetType));
            ilGen.Emit(OpCodes.Ret);

            pb.SetGetMethod(mb);
        }

        private void InjectsSubjectsToImpl(Type realType, T impl)
        {
            foreach (var kv in subjectsToInject)
            {
                realType.GetField(kv.Key, BindingFlags.Instance |
                                            BindingFlags.NonPublic)
                        .SetValue(impl, kv.Value);
            }
        }

        IEnumerable<PropertyInfo> GetTypeObservableProperties()
        {
            return typeof(T).GetProperties()
                            .Where(p => p.PropertyType
                                         .GetGenericTypeDefinition() == typeof(IObservable<>));
        }


        private void EnsureTypeIsValid()
        {
            var t = typeof(T);

            if (!t.IsInterface)
                throw new InvalidOperationException("Message handler must be an interface");

            foreach (var method in t.GetMethods())
            {
                if (!(method.IsSpecialName &&
                      method.Name.StartsWith("get_")))
                {
                    throw new InvalidOperationException("Message handler must not contain any methods");
                }
            }

            foreach (var property in t.GetProperties())
            {
                EnsureIsValidProperty(property);
            }
        }

        private void EnsureIsValidProperty(PropertyInfo property)
        {
            if (property.CanWrite)
                throw new InvalidOperationException(
                    string.Format(
                        "Property {0} cannot be writeable.", property.Name));

            var genericTypes = property.PropertyType.GenericTypeArguments;

            if (genericTypes.Length != 1)
                throw new InvalidOperationException(
                    string.Format(
                        "Property {0} in interface has invalid type {1} (it should be IObservable<T>)",
                        property.Name, property.PropertyType));

            var intType = genericTypes[0];

            if (intType.GetCustomAttribute<StacksMessageAttribute>() == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Property {0} has invalid type. It is IObservable<T>, but T does not have StacksMessage attribute",
                        property.Name));
            }
        }

        private Action<MemoryStream> SetupDeserializeAction<V>(Subject<V> subject)
        {
            return ms =>
            {
                var t = this.packetSerializer.Deserialize<V>(ms);
                subject.OnNext(t);
            };
        }
    }
}
