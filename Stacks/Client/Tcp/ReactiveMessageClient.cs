using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Tcp
{
    public class ReactiveMessageClient<T> : MessageClientBase
    {
        public T Packets { get; private set; }

        public ReactiveMessageClient(IFramedClient framedClient,
                                     IStacksSerializer packetSerializer)
            : base(framedClient, new MessageIdCache(), packetSerializer)
        {
            this.framedClient.Received.Subscribe(PacketReceived);

            Foo();
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int messageId = *((int*)b);
                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 4, buffer.Count - 4))
                {
                    //this.packetSerializationHandler.Deserialize(messageId, ms);
                }
            }
        }

        private void Foo()
        {
            var asmName = new AssemblyName("MessageClient_" + typeof(T).Name);
            var dynAsm = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);

            var typeB = dynAsm.DefineDynamicModule("MessageClient_" + typeof(T).Name + ".dll")
                              .DefineType(typeof(T).Name + "Impl", TypeAttributes.Public,
                                          null, new[] { typeof(T)});

            foreach (var property in typeof(T).GetProperties()
                                                .Where(p => p.PropertyType.GetGenericTypeDefinition() == typeof(IObservable<>)))
            {
                var obsType = property.PropertyType.GetGenericArguments().First();

                var propB = typeB.DefineProperty(property.Name, PropertyAttributes.HasDefault,
                                    property.PropertyType, null);

                var mb = typeB.DefineMethod("get_" + property.Name,
                                                MethodAttributes.Public |
                                                    MethodAttributes.SpecialName |
                                                    MethodAttributes.HideBySig | 
                                                    MethodAttributes.Virtual, 
                                                property.PropertyType, 
                                                Type.EmptyTypes);
                var ilGen = mb.GetILGenerator();

                ilGen.Emit(OpCodes.Ldnull);
                ilGen.Emit(OpCodes.Ret);

                propB.SetGetMethod(mb);
            }


            var myType = typeB.CreateType();

            dynAsm.Save(asmName.Name + ".dll");
            var t = (T)Activator.CreateInstance(myType);
        }
    }
}
