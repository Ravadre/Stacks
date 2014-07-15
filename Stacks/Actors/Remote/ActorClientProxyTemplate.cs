using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stacks;
using Stacks.Tcp;

namespace Stacks.Actors
{
    public abstract class ActorClientProxyTemplate
    {
        private IPEndPoint endPoint;
        private ActorRemoteMessageClient client;
        private ActionBlockExecutor exec;
        private IStacksSerializer serializer;

        private Dictionary<long, Action<MemoryStream, Exception>> replyHandlersByRequest;
        private long requestId;

        public ActorClientProxyTemplate(IPEndPoint endPoint)
        {
            this.endPoint = endPoint;

            serializer = new ProtoBufStacksSerializer();
            replyHandlersByRequest = new Dictionary<long, Action<MemoryStream, Exception>>();
            exec = new ActionBlockExecutor();
            client = new ActorRemoteMessageClient(
                        new FramedClient(
                            new SocketClient(exec)));

            client.MessageReceived += MessageReceived;
            client.Disconnected.Subscribe(HandleDisconnection);

            client.Connect(endPoint);
        }

        private void MessageReceived(string msgName, long requestId, MemoryStream ms)
        {
            Action<MemoryStream, Exception> handler;

            if (!replyHandlersByRequest.TryGetValue(requestId, out handler))
                return;
            replyHandlersByRequest.Remove(requestId);

            handler(ms, null);
        }

        private void HandleDisconnection(Exception exn)
        {
            foreach (var handler in replyHandlersByRequest)
            {
                var h = handler.Value;
                h(null, exn);
            }
            replyHandlersByRequest.Clear();
        }

        protected Task<R> SendMessage<T, R, P>(string msgName, T packet)
        {
            var reqId = GetRequestId();
            client.Send(msgName, reqId, packet);

            var tcs = new TaskCompletionSource<R>();

            exec.Enqueue(() =>
                {
                    replyHandlersByRequest[reqId] = (ms, error) =>
                        {
                            if (error == null)
                            {
                                try
                                {
                                    var p = (IReplyMessage<R>)serializer.Deserialize<P>(ms);
                                    var v = p.GetResult();
                                    tcs.SetResult(v);
                                }
                                catch (Exception exc)
                                {
                                    tcs.SetException(exc);
                                }
                            }
                            else
                            {
                                tcs.SetException(error);
                            }
                        };
                });

            return tcs.Task;
        }

        private long GetRequestId()
        {
            return Interlocked.Increment(ref requestId);
        }
    }
}
