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
using System.Reactive.Subjects;
using Stacks.Actors.Proto;
using Stacks.Actors.Remote;

namespace Stacks.Actors
{
    public abstract class ActorClientProxyTemplate<T> : IActorClientProxy<T>, IDisposable
    {
        private IPEndPoint endPoint;
        private ActorRemoteMessageClient client;
        private ActionBlockExecutor exec;
        protected ActorPacketSerializer serializer;

        private Dictionary<long, Action<MemoryStream, Exception>> replyHandlersByRequest;
        private long requestId;
        private bool disconnected;
        private Exception disconnectedException;

        private AsyncSubject<Exception> disconnectedSubject;
        public IObservable<Exception> Disconnected { get { return disconnectedSubject.AsObservable(); } }

        protected Dictionary<string, Action<ActorProtocolFlags, string, MemoryStream>> obsHandlers;

        public abstract T Actor { get; }

        public ActorClientProxyTemplate(IPEndPoint endPoint, ActorClientProxyOptions options)
        {
            this.endPoint = endPoint;
            this.disconnected = false;

            this.disconnectedSubject = new AsyncSubject<Exception>();
            serializer = options.SerializerProvider == null
                ? new ActorPacketSerializer(new ProtoBufStacksSerializer())
                : options.SerializerProvider(new ProtoBufStacksSerializer());
                 
            replyHandlersByRequest = new Dictionary<long, Action<MemoryStream, Exception>>();
            obsHandlers = new Dictionary<string, Action<ActorProtocolFlags, string, MemoryStream>>();
            exec = new ActionBlockExecutor();
            exec.Error += ExecutionError;
            client = new ActorRemoteMessageClient(
                        serializer,
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

        internal async Task<IActorClientProxy<T>> Connect()
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
            Action<ActorProtocolFlags, string, MemoryStream> handler;

            if (!obsHandlers.TryGetValue(name, out handler))
                return;

            handler(ActorProtocolFlags.Observable, name, ms);
        }

        private void HandleDisconnection(Exception exn)
        {
            foreach (var handler in replyHandlersByRequest)
            {
                var h = handler.Value;
                h(null, exn);
            }
            replyHandlersByRequest.Clear();

            disconnected = true;
            disconnectedException = exn;

            disconnectedSubject.OnNext(exn);
            disconnectedSubject.OnCompleted();
        }

        protected Task<R> SendMessage<S, R, P>(string msgName, S packet)
        {
            var tcs = new TaskCompletionSource<R>();

            exec.Enqueue(() =>
            {
                var reqId = GetRequestId();

                try
                {
                    client.Send(msgName, reqId, packet);
                }
                catch (Exception exn)
                {
                    tcs.SetException(exn);
                    return;
                }

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
                                var p = (IReplyMessage<R>)serializer.Deserialize<P>(ActorProtocolFlags.RequestReponse, msgName, ms);
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
