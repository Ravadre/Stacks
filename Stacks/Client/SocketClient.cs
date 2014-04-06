using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;

using Stacks.Executors;

using NLog;

namespace Stacks
{
    public class SocketClient : IRawByteClient
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        private readonly IExecutor executor;

        private readonly Socket socket;

        private IPEndPoint remoteEndPoint;
        private IPEndPoint localEndPoint;

        private SocketAsyncEventArgs recvArgs;
        private byte[] recvBuffer;
        private const int recvBufferLength = 8192;

        private SocketAsyncEventArgs sendArgs;
        private IList<ArraySegment<byte>> toSendBuffers;
        private IList<ArraySegment<byte>> sendingBuffers;
        private bool isSending;

        private SocketAsyncEventArgs connectArgs;

        public event Action Connected;
        public event Action<Exception> Disconnected;
        public event Action<ArraySegment<byte>> Received;
        public event Action<int> Sent;

        public IPEndPoint RemoteEndPoint { get { return remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return localEndPoint; } }

        private bool disconnectionNotified;
        private bool wasConnected;

        public SocketClient(IExecutor executor, Socket socket)
        {
            this.executor = executor;
            this.socket = socket;
            this.wasConnected = true;

            InitialiseConnectedSocket();
            executor.Enqueue(StartReceiving);
        }

        public SocketClient(IExecutor executor)
        {
            this.executor = executor;
            this.socket = new Socket(AddressFamily.InterNetwork,
                                     SocketType.Stream,
                                     ProtocolType.Tcp);
            this.wasConnected = false;
        }

        public void Connect(IPEndPoint remoteEndPoint)
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
            this.remoteEndPoint = new IPEndPoint(0, 0);
            this.remoteEndPoint = (IPEndPoint)this.remoteEndPoint.Create(socket.RemoteEndPoint.Serialize());
            this.localEndPoint = new IPEndPoint(0, 0);
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
                    AddBufferToBufferList(buffer);

                    if (!isSending)
                    {
                        StartSending();
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

                var tmp = this.sendingBuffers;
                this.sendingBuffers = this.toSendBuffers;
                this.toSendBuffers = tmp;
                this.toSendBuffers.Clear();

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
            var h = Received;
            if (h != null)
            {
                try
                {
                    h(new ArraySegment<byte>(recvArgs.Buffer, 0, recvArgs.BytesTransferred));
                }
                catch { }
            }
        }

        private void HandleDisconnection(Exception exc)
        {
            if (disconnectionNotified)
                return;
            disconnectionNotified = true;
            
            if (exc != null)
            {
                log.Info("Client disconnected. " + exc.Message);
            }
            else
            {
                log.Info("Client disconnected (gracefully).");
            }

            SafeCloseSocket();
            OnDisconnected(exc);
        }

        private void OnDisconnected(Exception e)
        {
            var h = Disconnected;

            if (h != null)
            {
                try { h(e); }
                catch { }
            }
        }

        private void OnDataSent(int transferred)
        {
            var h = Sent;

            if (h != null)
            {
                try { h(transferred); }
                catch { }
            }
        }

        private void OnConnected()
        {
            var h = Connected;

            if (h != null)
            {
                try { h(); }
                catch { }
            }
        }

    }
}
