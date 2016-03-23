using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Stacks.Actors.Proto;
using Stacks.Actors.Remote;
using Stacks.Tcp;

namespace Stacks.Actors
{
    public abstract class ActorClientProxyTemplate<T> : IActorClientProxy<T>, IDisposable
    {
        private readonly ActorRemoteMessageClient client;
        private readonly AsyncSubject<Exception> disconnectedSubject;
        private readonly IPEndPoint endPoint;
        private readonly ActionBlockExecutor exec;
        private readonly Dictionary<long, Action<MemoryStream, Exception>> replyHandlersByRequest;
        private bool disconnected;
        private Exception disconnectedException;
        protected Dictionary<string, Action<ActorProtocolFlags, string, MemoryStream>> obsHandlers;
        private long requestId;
        protected ActorPacketSerializer serializer;
        // ReSharper disable once PublicConstructorInAbstractClass
        public ActorClientProxyTemplate(IPEndPoint endPoint, ActorClientProxyOptions options)
        {
            this.endPoint = endPoint;
            disconnected = false;

            disconnectedSubject = new AsyncSubject<Exception>();
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

        public IObservable<Exception> Disconnected => disconnectedSubject.AsObservable();
        public abstract T Actor { get; }

        public void Close()
        {
            client.Close();
        }

        public void Dispose()
        {
            Close();
        }

        private void ExecutionError(Exception exn)
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
                handler.Value(null, exn);
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
                            var p =
                                (IReplyMessage<R>)
                                    serializer.Deserialize<P>(ActorProtocolFlags.RequestReponse, msgName, ms);
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