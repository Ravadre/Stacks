using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Serializers
{
    public abstract class BaseStacksSerializer : IStacksSerializer
    {
        private Dictionary<int, Action<MemoryStream>> serializerHandlerByTypeCode;

        private IMessageHandler messageHandler;

        public BaseStacksSerializer(IMessageHandler messageHandler)
        {
            this.serializerHandlerByTypeCode = new Dictionary<int, Action<MemoryStream>>();
            this.messageHandler = messageHandler;

            Initialize();

            ParseMessageHandler();
        }

        protected virtual void Initialize() { }
        
        public virtual void PrepareSerializerForType<T>() { }
        
        public abstract void Serialize<T>(T obj, MemoryStream ms);

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
            var createLambdaMethod = typeof(BaseStacksSerializer)
                                         .GetMethod("CreateLambda", BindingFlags.Instance | BindingFlags.NonPublic)
                                         .MakeGenericMethod(type);

            this.GetType()
                .GetMethod("PrepareSerializerForType")
                .MakeGenericMethod(type)
                .Invoke(this, Type.EmptyTypes);
                    

            return (Action<MemoryStream>)createLambdaMethod.Invoke(this, new[] { mi });
        }

        private Action<MemoryStream> CreateLambda<T>(MethodInfo mi)
        {
            var handler = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), messageHandler, mi);

            var deserializer = CreateDeserializer<T>();

            return ms =>
            {
                var obj = deserializer(ms);
                handler(obj);
            };
        }

        protected abstract Func<MemoryStream, T> CreateDeserializer<T>();
    }
}
