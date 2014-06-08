using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Stacks.Tcp;

namespace Stacks
{
    public class StacksSerializationHandler
    {
        private Dictionary<int, Action<MemoryStream>> serializerHandlerByMessageId;

        private IMessageIdCache messageIdCache;

        private IMessageHandler messageHandler;
        private IStacksSerializer serializer;
        private IMessageClient client;

        public StacksSerializationHandler(
                    IMessageIdCache messageIdCache,
                    IMessageClient client, 
                    IStacksSerializer serializer, 
                    IMessageHandler messageHandler)
        {
            Ensure.IsNotNull(messageIdCache, "messageIdCache");
            Ensure.IsNotNull(client, "client");
            Ensure.IsNotNull(serializer, "serializer");
            Ensure.IsNotNull(messageHandler, "messageHandler");

            this.messageIdCache = messageIdCache;
            this.client = client;
            this.serializerHandlerByMessageId = new Dictionary<int, Action<MemoryStream>>();
            this.messageHandler = messageHandler;
            this.serializer = serializer;

            this.serializer.Initialize();

            ParseMessageHandler();
        }

        public void Serialize<T>(T obj, MemoryStream ms)
        {
            this.serializer.Serialize(obj, ms);
        }

        public void Deserialize(int messageId, MemoryStream ms)
        {
            Action<MemoryStream> handler;

            if (serializerHandlerByMessageId.TryGetValue(messageId, out handler))
            {
                handler(ms);
            }
            else
            {
                throw new InvalidOperationException(
                    string.Format("No registered message handler for message id {0}", messageId));
            }
        }

      

        private void ParseMessageHandler()
        {
            this.serializerHandlerByMessageId = new Dictionary<int,Action<MemoryStream>>();

            foreach (var mi in messageHandler.GetType()
                                             .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                             .Where(IsValidMessageHandlerMethod))
            {
                var paramType = mi.GetParameters()[1].ParameterType;
                messageIdCache.PreLoadType(paramType);

                var serializer = CreateSerializerForType(paramType, mi);

                this.serializerHandlerByMessageId[messageIdCache.GetMessageId(paramType)] = serializer;
            }
        }

        private static bool IsValidMessageHandlerMethod(MethodInfo mi)
        {
            if (mi.ReturnType != typeof(void))
                return false;

            if (mi.IsConstructor)
                return false;

            if (mi.GetParameters().Length != 2)
                return false;

            if (!typeof(IMessageClient).IsAssignableFrom(mi.GetParameters()[0].ParameterType))
                return false;

            return true;
        }

        private Action<MemoryStream> CreateSerializerForType(Type type, MethodInfo mi)
        {
            var createLambdaMethod = typeof(StacksSerializationHandler)
                                         .GetMethod("CreateLambda", BindingFlags.Instance | BindingFlags.NonPublic)
                                         .MakeGenericMethod(mi.GetParameters()[0].ParameterType, type);

            this.serializer
                .GetType()
                .GetMethod("PrepareSerializerForType")
                .MakeGenericMethod(type)
                .Invoke(this.serializer, Type.EmptyTypes);
                    

            return (Action<MemoryStream>)createLambdaMethod.Invoke(this, new[] { mi });
        }

        private Action<MemoryStream> CreateLambda<T, Y>(MethodInfo mi)
        {
            var handler = (Action<T, Y>)Delegate.CreateDelegate(typeof(Action<T, Y>), messageHandler, mi);

            return ms =>
            {
                var obj = this.serializer.Deserialize<Y>(ms);
                handler((T)client, obj);
            };
        }
    }
}
