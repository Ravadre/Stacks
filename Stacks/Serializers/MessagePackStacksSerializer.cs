using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MsgPack;
using MsgPack.Serialization;
using System.IO;
using System.Reflection;

namespace Stacks.Serializers
{
    public class MessagePackStacksSerializer : IStacksSerializer
    {
        private SerializationContext context;
        private Dictionary<int, Action<MemoryStream>> serializerHandlerByTypeCode;

        private IMessageHandler messageHandler;

        public MessagePackStacksSerializer(IMessageHandler messageHandler)
        {
            this.messageHandler = messageHandler;
            this.context = new SerializationContext();

            ParseMessageHandler();
        }

        public void Deserialize(int typeCode, MemoryStream ms)
        {
            Action<MemoryStream> handler;
            
            if (serializerHandlerByTypeCode.TryGetValue(typeCode, out handler))
            {
                handler(ms);
            }
            else
            {
                throw new InvalidOperationException(
                    string.Format("No registered message handler for type code {0}", typeCode));
            }
        }

        public void Serialize<T>(T obj, MemoryStream ms)
        {
            var serializer = MessagePackSerializer.Create<T>(context);
            serializer.Pack(ms, obj);
        }

        private void ParseMessageHandler()
        {
            this.serializerHandlerByTypeCode = messageHandler.GetType()
                          .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                          .Where(IsValidMessageHandlerMethod)
                          .Select(mi => Tuple.Create(GetTypeCode(mi), GetMessageHandlerParameterType(mi), mi))
                          .Select(t => Tuple.Create(t.Item1, CreateSerializerForType(t.Item2, t.Item3)))
                          .ToDictionary(t => t.Item1, t => t.Item2);
        }

        private static bool IsValidMessageHandlerMethod(MethodInfo mi)
        {
            if (!HasMessageHandlerAttribute(mi))
                return false;

            if (mi.ReturnType != typeof(void))
                return false;

            if (mi.IsConstructor)
                return false;

            if (mi.GetParameters().Length != 1)
                return false;

            return true;
        }

        private static bool HasMessageHandlerAttribute(MethodInfo mi)
        {
            return mi.GetCustomAttribute<MessageHandlerAttribute>(true) != null;
        }

        private static int GetTypeCode(MethodInfo mi)
        {
            return mi.GetCustomAttribute<MessageHandlerAttribute>().TypeCode;
        }

        private static Type GetMessageHandlerParameterType(MethodInfo mi)
        {
            return mi.GetParameters()[0].ParameterType;
        }

        private Action<MemoryStream> CreateSerializerForType(Type type, MethodInfo mi)
        {
            var createLambdaMethod = this.GetType()
                                         .GetMethod("CreateLambda", BindingFlags.Instance | BindingFlags.NonPublic)
                                         .MakeGenericMethod(type);

            return (Action<MemoryStream>)createLambdaMethod.Invoke(this, new[] { mi });                             
        }

        private Action<MemoryStream> CreateLambda<T>(MethodInfo mi)
        {
            var handler = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), messageHandler, mi);
            var serializer = MsgPack.Serialization.MessagePackSerializer.Create<T>(context);

            return ms =>
            {
                var obj = serializer.Unpack(ms);
                handler(obj);
            };
        }
    }
}
