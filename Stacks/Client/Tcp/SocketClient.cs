using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq; 
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Tcp
{
    public class SocketClient : IRawByteClient
    {
        private IExecutor executor;

        private readonly Socket socket;

        private IPEndPoint remoteEndPoint;
        private IPEndPoint localEndPoint;

        private SocketAsyncEventArgs recvArgs;
        private byte[] recvBuffer;
        private const int recvBufferLength = 8192;

        private SocketAsyncEventArgs sendArgs;
        private List<ArraySegment<byte>> toSendBuffers;
        private List<ArraySegment<byte>> sendingBuffers;
        private bool isSending;

        private SocketAsyncEventArgs connectArgs;

        private AsyncSubject<Unit> connected;
        private AsyncSubject<Exception> disconnected;
        private Subject<int> sent;
        private Subject<ArraySegment<byte>> received;

        public IObservable<Unit> Connected { get { return connected.AsObservable(); } }
        public IObservable<Exception> Disconnected { get { return disconnected.AsObservable(); } }
        public IObservable<int> Sent { get { return sent.AsObservable(); } }
        public IObservable<ArraySegment<byte>> Received { get { return received.AsObservable(); } }

        public IPEndPoint RemoteEndPoint { get { return remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return localEndPoint; } }

        public Socket Socket { get { return socket; } }

        private bool disconnectionNotified;
        private bool wasConnected;

        public bool IsConnected { get { return socket.Connected; } }

        public IExecutor Executor { get { return executor; } }

        public SocketClient(IExecutor executor, Socket socket)
        {
            InitialiseCommon(executor);

            this.socket = socket;
            this.wasConnected = true;

            EnsureSocketIsConnected();
         
            InitialiseConnectedSocket();           
        }

        private void EnsureSocketIsConnected()
        {
            if (!socket.Connected)
                throw new InvalidOperationException("Socket must be connected");
        }

        public SocketClient()
            : this(new ActionBlockExecutor(), false)
        { }

        public SocketClient(bool useIPv6)
            : this(new ActionBlockExecutor(), useIPv6)
        { }

        public SocketClient(IExecutor executor)
            : this(executor, false)
        { }

        public SocketClient(IExecutor executor, bool useIPv6)
        {
            InitialiseCommon(executor);

            var family = useIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
            
            this.socket = new Socket(family,
                                     SocketType.Stream,
                                     ProtocolType.Tcp);
            this.wasConnected = false;
        }

        private void InitialiseCommon(IExecutor executor)
        {
            this.connected = new AsyncSubject<Unit>();
            this.disconnected = new AsyncSubject<Exception>();
            this.sent = new Subject<int>();
            this.received = new Subject<ArraySegment<byte>>();

            this.executor = executor;
        }

        public IObservable<Unit> Connect(string remoteEndPoint)
        {
            return Connect(IPHelpers.Parse(remoteEndPoint));
        }

        public IObservable<Unit> Connect(IPEndPoint remoteEndPoint)
        {
            if (this.wasConnected)
                throw new InvalidOperationException("Socket was already in connected state");
            this.wasConnected = true;

            connectArgs = new SocketAsyncEventArgs();
            connectArgs.Completed += ConnectedCapture;
            connectArgs.RemoteEndPoint = remoteEndPoint;

            bool isPending = this.socket.ConnectAsync(connectArgs);

            if (!isPending)
                ConnectedCapture(this, this.connectArgs);

            return Connected;
        }

        private void ConnectedCapture(object sender, SocketAsyncEventArgs e)
        {
            executor.Enqueue(() => HandleConnected(sender, e));
        }

        private void HandleConnected(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError == SocketError.Success)
                {
                    InitialiseConnectedSocket();

                    OnConnected();

                    StartReceiving();
                }
                else
                {
                    HandleDisconnection(new SocketException((int)e.SocketError));
                }
            }
            catch (Exception exc)
            {
                HandleDisconnection(exc);
            }
        }

        private void InitialiseConnectedSocket()
        {
            recvBuffer = new byte[recvBufferLength];
            recvArgs = new SocketAsyncEventArgs();
            recvArgs.SetBuffer(recvBuffer, 0, recvBufferLength);
            recvArgs.Completed += DataReceivedCapture;

            sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += DataSentCapture;
            toSendBuffers = new List<ArraySegment<byte>>();
            sendingBuffers = new List<ArraySegment<byte>>();

            disconnectionNotified = false;
            isSending = false;

            CopyEndPoints();
        }

        internal void ScheduleStartReceiving()
        {
            executor.Enqueue(StartReceiving);
        }

        private void StartReceiving()
        {
            try
            {
                recvArgs.SetBuffer(0, recvBufferLength);
                bool isPending = socket.ReceiveAsync(recvArgs);

                if (!isPending)
                    DataReceived(this, recvArgs);
            }
            catch (Exception exc)
            {
                HandleDisconnection(exc);
            }
        }

        private void DataReceivedCapture(object sender, SocketAsyncEventArgs e)
        {
            executor.Enqueue(() => DataReceived(sender, e));
        }

        private void DataReceived(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError == SocketError.Success)
                {
                    int transferred = e.BytesTransferred;

                    if (transferred == 0)
                    {
                        //Graceful disconnection
                        HandleDisconnection(new SocketException((int)SocketError.Disconnecting));

                        return;
                    }

                    OnDataReceived();
                }
                else
                {
                    HandleDisconnection(new SocketException((int)e.SocketError));
                    return;
                }
            }
            catch (Exception exc)
            {
                HandleDisconnection(exc);
                return;
            }

            StartReceiving();
        }

        private void CopyEndPoints()
        {
            IPAddress ip;
            if (this.socket.AddressFamily == AddressFamily.InterNetworkV6)
                ip = IPAddress.IPv6Any;
            else
                ip = IPAddress.Any;

            this.remoteEndPoint = new IPEndPoint(ip, 0);
            this.remoteEndPoint = (IPEndPoint)this.remoteEndPoint.Create(socket.RemoteEndPoint.Serialize());
            this.localEndPoint = new IPEndPoint(ip, 0);
            this.localEndPoint = (IPEndPoint)this.localEndPoint.Create(socket.LocalEndPoint.Serialize());
        }

        private void SafeCloseSocket()
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch { }

            try
            {
                socket.Close();
            }
            catch { }
        }

        public void Send(byte[] buffer)
        {
            Send(new ArraySegment<byte>(buffer));
        }

        public void Send(ArraySegment<byte> buffer)
        {
            Ensure.IsNotNull(buffer.Array, "buffer.Array");

            executor.Enqueue(() =>
                {
                    try
                    {
                        AddBufferToBufferList(buffer);

                        if (!isSending)
                        {
                            StartSending();
                        }
                    }
                    catch (Exception exn)
                    {
                        HandleDisconnection(exn);
                    }
                });
        }

        private void AddBufferToBufferList(ArraySegment<byte> buffer)
        {
            toSendBuffers.Add(buffer);
        }

        private void StartSending()
        {
            try
            {
                if (toSendBuffers.Count == 0)
                    return;

                if (this.toSendBuffers.Count > 1024)
                {
                    var toTake = Math.Min(1024, this.toSendBuffers.Count);
                    var swap = this.toSendBuffers.Take(toTake).ToList();
                    this.toSendBuffers.RemoveRange(0, toTake);
                    this.sendingBuffers.Clear();
                    this.sendingBuffers = swap;
                }
                else
                {
                    var tmp = this.sendingBuffers;
                    this.sendingBuffers.Clear();
                    this.sendingBuffers = this.toSendBuffers;
                    this.toSendBuffers = tmp;
                }

                this.sendArgs.BufferList = this.sendingBuffers;

                bool isPending = this.socket.SendAsync(sendArgs);

                if (!isPending)
                    DataSent(this, sendArgs);
                else
                    isSending = true;
            }
            catch (Exception exc)
            {
                HandleDisconnection(exc);
            }
        }

        private void DataSentCapture(object sender, SocketAsyncEventArgs e)
        {
            executor.Enqueue(() => DataSent(sender, e));
        }

        private void DataSent(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                isSending = false;

                if (e.SocketError == SocketError.Success)
                {
                    int transferred = e.BytesTransferred;

                    OnDataSent(transferred);
                }
                else
                {
                    HandleDisconnection(new SocketException((int)e.SocketError));
                    return;
                }
            }
            catch (Exception exc)
            {
                HandleDisconnection(exc);
                return;
            }

            StartSending();
        }

        public void Close()
        {
            SafeCloseSocket();
        }

        private void OnDataReceived()
        {
            received.OnNext(new ArraySegment<byte>(recvArgs.Buffer, 0, recvArgs.BytesTransferred));
        }

        private void HandleDisconnection(Exception exc)
        {
            if (disconnectionNotified)
                return;
            disconnectionNotified = true;

            SafeCloseSocket();
            OnDisconnected(exc);
        }

        private void OnDisconnected(Exception e)
        {
            disconnected.OnNext(e);
            disconnected.OnCompleted();

            connected.OnError(e);
        }

        private void OnDataSent(int transferred)
        {
            this.sent.OnNext(transferred);
        }

        private void OnConnected()
        {
            connected.OnNext(Unit.Default);
            connected.OnCompleted();
        }
    }
}
