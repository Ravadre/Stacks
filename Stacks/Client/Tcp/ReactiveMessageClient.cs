using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Tcp
{
    public class ReactiveMessageClient<T> : MessageClientBase
    {
        private Dictionary<int, Action<MemoryStream>> deserializeByMessageId;


        public T Packets { get; private set; }

        public ReactiveMessageClient(IFramedClient framedClient,
                                     IStacksSerializer packetSerializer)
            : base(framedClient, new MessageIdCache(), packetSerializer)
        {
            this.deserializeByMessageId = new Dictionary<int, Action<MemoryStream>>();

            this.framedClient.Received.Subscribe(PacketReceived);

            PrepareReceiverImplementation();
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            Action<MemoryStream> handler;
            
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int messageId = *((int*)b);
                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 4, buffer.Count - 4))
                {
                    if (deserializeByMessageId.TryGetValue(messageId, out handler))
                    {
                        handler(ms);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            string.Format("No registered message handler for message id {0}", messageId));
                    }
                }
            }
        }

        private Dictionary<string, object> subjectsToInject;
        private TypeBuilder tb;

        private void PrepareReceiverImplementation()
        {
            tb = CreateTypeBuilder();
            subjectsToInject = new Dictionary<string, object>();

            foreach (var property in GetTypeObservableProperties())
            {
                ParseProperty(property);
            }

            var myType = tb.CreateType();

            var packetsImplementation = (T)Activator.CreateInstance(myType);

            InjectsSubjectsToImpl(myType, packetsImplementation);

            this.Packets = packetsImplementation;
        }

        private void SetupDeserializeAction<V>(int messageId, Subject<V> subject)
        {
            this.deserializeByMessageId[messageId] = ms =>
                {
                    var t = base.packetSerializer.Deserialize<V>(ms);
                    subject.OnNext(t);
                };
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

        private void ParseProperty(PropertyInfo property)
        {
            var packetType = property.PropertyType.GetGenericArguments().First();
            int messageId = base.messageIdCache.GetMessageId(packetType);

            var subjectType = typeof(Subject<>).MakeGenericType(packetType);
            var subject = Activator.CreateInstance(subjectType);

            var pName = property.Name;
            var fieldName = pName + "field";

            subjectsToInject[fieldName] = subject;

            typeof(ReactiveMessageClient<T>)
                .GetMethod("SetupDeserializeAction", BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(packetType)
                .Invoke(this, new[] { messageId, subject });


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

            ilGen.Emit(OpCodes.Call, typeof(Observable).GetMethod("AsObservable", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(packetType));
            ilGen.Emit(OpCodes.Ret);

            pb.SetGetMethod(mb);
        }

        private void InjectsSubjectsToImpl(Type realType, T impl)
        {
            foreach (var kv in subjectsToInject)
            {
                realType.GetField(kv.Key, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(impl, kv.Value);
            }
        }

        IEnumerable<PropertyInfo> GetTypeObservableProperties()
        {
            return typeof(T).GetProperties()
                            .Where(p => p.PropertyType.GetGenericTypeDefinition() == typeof(IObservable<>));
        }
    }
}
