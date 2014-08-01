using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stacks;
using System.Reactive.Linq;
using Stacks.Tcp;

namespace Stacks.Actors
{
    public abstract class ActorClientProxyTemplate : IActorClientProxy, IDisposable
    {
        private IPEndPoint endPoint;
        private ActorRemoteMessageClient client;
        private ActionBlockExecutor exec;
        protected IStacksSerializer serializer;

        private Dictionary<long, Action<MemoryStream, Exception>> replyHandlersByRequest;
        private long requestId;
        private bool disconnected;
        private Exception disconnectedException;
        
        protected Dictionary<string, Action<MemoryStream>> obsHandlers;

        public ActorClientProxyTemplate(IPEndPoint endPoint)
        {
            this.endPoint = endPoint;
            this.disconnected = false;

            serializer = new ProtoBufStacksSerializer();
            replyHandlersByRequest = new Dictionary<long, Action<MemoryStream, Exception>>();
            obsHandlers = new Dictionary<string, Action<MemoryStream>>();
            exec = new ActionBlockExecutor();
            exec.Error += ExecutionError;
            client = new ActorRemoteMessageClient(
                        new FramedClient(
                            new SocketClient(exec)));

            client.MessageReceived += MessageReceived;
            client.ObsMessageReceived += ObsMessageReceived;
            client.Disconnected.Subscribe(HandleDisconnection);
        }

        void ExecutionError(Exception exn)
        {
            HandleDisconnection(exn);
        }

        internal async Task<ActorClientProxyTemplate> Connect()
        {
            await client.Connect(endPoint);
            return this;
        }

        private void MessageReceived(long requestId, MemoryStream ms)
        {
            Action<MemoryStream, Exception> handler;

            if (!replyHandlersByRequest.TryGetValue(requestId, out handler))
                return;
            replyHandlersByRequest.Remove(requestId);

            handler(ms, null);
        }

        private void ObsMessageReceived(string name, MemoryStream ms)
        {
            Action<MemoryStream> handler;

            if (!obsHandlers.TryGetValue(name, out handler))
                return;

            handler(ms);
        }

        private void HandleDisconnection(Exception exn)
        {
            foreach (var handler in replyHandlersByRequest)
            {
                var h = handler.Value;
                h(null, exn);
            }
            replyHandlersByRequest.Clear();
            this.disconnected = true;
            this.disconnectedException = exn;
        }

        protected Task<R> SendMessage<T, R, P>(string msgName, T packet)
        {
            var reqId = GetRequestId();
            client.Send(msgName, reqId, packet);

            var tcs = new TaskCompletionSource<R>();

            exec.Enqueue(() =>
                {
                    if (disconnected)
                    {
                        tcs.SetException(disconnectedException);
                        return;
                    }

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
                    return;
                });

            return tcs.Task;
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            client.Close();
        }

        private long GetRequestId()
        {
            return Interlocked.Increment(ref requestId);
        }

    }
}
