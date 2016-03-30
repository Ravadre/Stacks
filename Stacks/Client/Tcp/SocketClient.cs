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
    /// <summary>
    /// Represents a single client socket. SocketClient can be used to send and receive
    /// raw data. It is a replacement for System.Net.Sockets.TcpClient.
    /// </summary>
    public class SocketClient : IRawByteClient
    {
        private IExecutor executor;

        private readonly Socket socket;

        private IPEndPoint remoteEndPoint;
        private IPEndPoint localEndPoint;

        private SocketAsyncEventArgs recvArgs;
        private byte[] recvBuffer;
        private const int recvBufferLength = 16384;

        private SocketAsyncEventArgs sendArgs;
        private LinkedList<List<ArraySegment<byte>>> toSendBuffers;
        private bool isSending;

        private SocketAsyncEventArgs connectArgs;

        private AsyncSubject<Unit> connected;
        private AsyncSubject<Exception> disconnected;
        private Subject<int> sent;
        private Subject<ArraySegment<byte>> received;

        /// <summary>
        /// Signalled, when socket is connected. Connected observable will be signalled
        /// as completed once socket is connected. If socket throws an error before it 
        /// connects, Connected will signal an error.
        /// <remarks>Because Connected will signal completion of error immediately, 
        /// it can be treated as awaitable.
        /// </remarks>
        /// </summary>
        public IObservable<Unit> Connected
        {
            get { return connected.AsObservable(); }
        }

        /// <summary>
        /// Signalled, when socket is disconnected. Disconnected will never fail, as disconnection
        /// reason is given as a completion result.
        /// <remarks>Because Disconnected will signal completion immediately, it can be treated as awaitable.
        /// </remarks>
        /// </summary>
        public IObservable<Exception> Disconnected
        {
            get { return disconnected.AsObservable(); }
        }

        /// <summary>
        /// Signalled every time socket finishes sending bytes. This doesn't mean that
        /// they are received by remote endpoint. Parameter is number of bytes sent.
        /// </summary>
        public IObservable<int> Sent
        {
            get { return sent.AsObservable(); }
        }

        /// <summary>
        /// Signalled when bytes are received. Received bytes are given as a parameter.
        /// </summary>
        public IObservable<ArraySegment<byte>> Received
        {
            get { return received.AsObservable(); }
        }

        /// <summary>
        /// Remote end point for socket. It is safe to access this property after disconnection.
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return remoteEndPoint; }
        }

        /// <summary>
        /// Local socket end point. It is safe to access this property after disconnection.
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return localEndPoint; }
        }

        private volatile bool disconnectionNotified;
        private volatile bool wasConnected;

        private Timer connectionTimeoutTimer;
        private bool connectionInProgress;

        /// <summary>
        /// Returns true, if socket is connected. 
        /// Returned value might not be accurate if no communication 
        /// was done recently.
        /// </summary>
        public bool IsConnected
        {
            get { return socket.Connected; }
        }

        /// <summary>
        /// Access underlying executor.
        /// </summary>
        public IExecutor Executor
        {
            get { return executor; }
        }

        /// <summary>
        /// Create new socket client using given executor and already connected socket.
        /// This method will fail, if socket is not already connected.
        /// </summary>
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

        /// <summary>
        /// Creates IPv4 tcp socket client with default executor.
        /// </summary>
        public SocketClient()
            : this(new ActionBlockExecutor(), false)
        {
        }

        /// <summary>
        /// Creates tcp socket client with default executor.
        /// </summary>
        public SocketClient(bool useIPv6)
            : this(new ActionBlockExecutor(), useIPv6)
        {
        }

        /// <summary>
        /// Creates IPv4 tcp socket client with given executor.
        /// </summary>
        public SocketClient(IExecutor executor)
            : this(executor, false)
        {
        }

        /// <summary>
        /// Creates tcp socket client with given executor.
        /// </summary>
        public SocketClient(IExecutor executor, bool useIPv6)
        {
            InitialiseCommon(executor);

            var family = useIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;

            this.socket = new Socket(family,
                SocketType.Stream,
                ProtocolType.Tcp);
            SetFastLoopbackOption(socket);
            socket.NoDelay = true;
            this.wasConnected = false;
            this.connectionTimeoutTimer = new Timer(OnConnectionTimeout, null, -1, -1);
        }

        private void InitialiseCommon(IExecutor executor)
        {
            this.connected = new AsyncSubject<Unit>();
            this.disconnected = new AsyncSubject<Exception>();
            this.sent = new Subject<int>();
            this.received = new Subject<ArraySegment<byte>>();

            this.executor = executor;
        }

        /// <summary>
        /// Connects to a given remote end point. This method returns before
        /// socket is connected, observe Connected property to discover
        /// if actual connection was successfull or not. 
        /// </summary>
        /// <param name="remoteEndPoint">
        /// End point in format [proto]://[address]:[port]
        /// <para>Supported protocols: 'tcp' and 'tcp6'</para>
        /// <para>Supported addresses: 'localhost' or numeric form.</para>
        /// </param>
        public IObservable<Unit> Connect(string remoteEndPoint)
        {
            return Connect(IPHelpers.Parse(remoteEndPoint));
        }

        /// <summary>
        /// Connects to a given remote end point. This method returns before
        /// socket is connected, observe Connected property to discover
        /// if actual connection was successfull or not. 
        /// </summary>
        public IObservable<Unit> Connect(IPEndPoint remoteEndPoint)
        {
            if (this.wasConnected)
                throw new InvalidOperationException("Socket was already in connected state");
            this.wasConnected = true;

            executor.Enqueue(() =>
            {
                connectArgs = new SocketAsyncEventArgs();
                connectArgs.Completed += ConnectedCapture;
                connectArgs.RemoteEndPoint = remoteEndPoint;

                bool isPending = this.socket.ConnectAsync(connectArgs);

                if (!isPending)
                    ConnectedCapture(this, this.connectArgs);
                else
                {
                    connectionInProgress = true;
                    connectionTimeoutTimer.Change(20000, -1);
                }
            });

            return Connected;
        }

        private void OnConnectionTimeout(object _)
        {
            executor.Enqueue(() =>
            {
                if (!connectionInProgress)
                    return;

                connectionInProgress = false;
                connectArgs.SocketError = SocketError.TimedOut;
                HandleConnected(null, connectArgs);
            });
        }

        private void ConnectedCapture(object sender, SocketAsyncEventArgs e)
        {
            executor.Enqueue(() => HandleConnected(sender, e));
        }

        private void HandleConnected(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                // If connection is not in progress, then probably we
                // have timed out, so make sure that only timed out error 
                // will be processed.
                if (!connectionInProgress &&
                    e.SocketError != SocketError.TimedOut)
                    return;

                connectionTimeoutTimer.Change(-1, -1);
                connectionTimeoutTimer.Dispose();
                connectionInProgress = false;

                // This seems to be broken on Mono. 
                // SocketError is success even when socket is not connected.
                // However, checking RemoteEndPoint seems to overcome this issue
                // (on .Net, this however throws when socket is not connected)
#if !MONO
                if (e.SocketError == SocketError.Success)
#else
                if (e.SocketError == SocketError.Success &&
                    socket.RemoteEndPoint != null)
#endif
                {
                    InitialiseConnectedSocket();

                    OnConnected();

                    StartReceiving();
                }
                else
                {
                    HandleDisconnection(new SocketException((int) e.SocketError));
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
            toSendBuffers = new LinkedList<List<ArraySegment<byte>>>();

            disconnectionNotified = false;
            isSending = false;

            CopyEndPoints();
        }

        private static void SetFastLoopbackOption(Socket socket)
        {
            //From StackExchange.Redis

            // SIO_LOOPBACK_FAST_PATH (http://msdn.microsoft.com/en-us/library/windows/desktop/jj841212%28v=vs.85%29.aspx)
            // Speeds up localhost operations significantly. OK to apply to a socket that will not be hooked up to localhost, 
            // or will be subject to WFP filtering.
            const int SIO_LOOPBACK_FAST_PATH = -1744830448;

#if !CORE_CLR
            // windows only
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Win8/Server2012+ only
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Major > 6 || osVersion.Major == 6 && osVersion.Minor >= 2)
                {
                    byte[] optionInValue = BitConverter.GetBytes(1);
                    socket.IOControl(SIO_LOOPBACK_FAST_PATH, optionInValue, null);
                }
            }
#else
            try
            {
                // Ioctl is not supported on other platforms at the moment
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    byte[] optionInValue = BitConverter.GetBytes(1);
                    socket.IOControl(SIO_LOOPBACK_FAST_PATH, optionInValue, null);
                }
            }
            catch (SocketException)
            {
            }
#endif
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
                        HandleDisconnection(new SocketException((int) SocketError.Disconnecting));

                        return;
                    }

                    OnDataReceived();
                }
                else
                {
                    HandleDisconnection(new SocketException((int) e.SocketError));
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
            var ip = socket.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

            remoteEndPoint = new IPEndPoint(ip, 0);
            remoteEndPoint = (IPEndPoint) remoteEndPoint.Create(socket.RemoteEndPoint.Serialize());
            localEndPoint = new IPEndPoint(ip, 0);
            localEndPoint = (IPEndPoint) localEndPoint.Create(socket.LocalEndPoint.Serialize());
        }

        private void SafeCloseSocket()
        {
            try { socket.Shutdown(SocketShutdown.Both); }
            catch { /* ignore */ }

            try { socket.Close(); }
            catch { /* ignore */ }

            try { recvArgs.Dispose(); }
            catch { /* ignore */ }

            try { sendArgs.Dispose(); }
            catch { /* ignore */ }

            try { connectArgs?.Dispose(); }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Enqueues buffer to be sent by the socket.
        /// </summary>
        public void Send(byte[] buffer) => Send(new ArraySegment<byte>(buffer));

        /// <summary>
        /// Enqueues buffer to be sent by the socket.
        /// Throws instantly only if buffer is null, otherwise, 
        /// any errors will be signalled by socket events.
        /// </summary>
        public void Send(ArraySegment<byte> buffer)
        {
            Ensure.IsNotNull(buffer.Array, $"{nameof(buffer)}.Array");

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
            if (this.toSendBuffers.Count == 0)
            {
                this.toSendBuffers.AddLast(new List<ArraySegment<byte>>(1024)
                {
                    buffer
                });
            }
            else
            {
                var lastBuffer = this.toSendBuffers.Last.Value;
                if (lastBuffer.Count < 1024)
                {
                    lastBuffer.Add(buffer);
                }
                else
                {
                    var newBuffer = new List<ArraySegment<byte>>(1024)
                    {
                        buffer
                    };
                    this.toSendBuffers.AddLast(newBuffer);
                }
            }
        }

        private void StartSending()
        {
            try
            {
                if (toSendBuffers.Count == 0)
                    return;
                var sendingBuffer = this.toSendBuffers.First.Value;
                this.toSendBuffers.RemoveFirst();
                this.sendArgs.BufferList = sendingBuffer;

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
                    HandleDisconnection(new SocketException((int) e.SocketError));
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

        /// <summary>
        /// Closes socket. This method will never throw, even is it can't be closed gracefully.
        /// </summary>
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