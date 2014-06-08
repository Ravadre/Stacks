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
            var asmName = new AssemblyName("MessageClient_" + typeof(T).Name);
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
