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

        private Socket socket;

        private IPEndPoint remoteEndPoint;
        private IPEndPoint localEndPoint;

        private SocketAsyncEventArgs recvArgs;
        private byte[] recvBuffer;
        private const int recvBufferLength = 8192;

        private SocketAsyncEventArgs sendArgs;
        private IList<ArraySegment<byte>> toSendBuffers;
        private IList<ArraySegment<byte>> sendingBuffers;
        private SpinLock sendBuffersLock;
        private int isSending;

        public event Action<ArraySegment<byte>> Received;
        public event Action<Exception> Disconnected;

        public IPEndPoint RemoteEndPoint { get { return remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return localEndPoint; } }

        private int disconnectionNotified;

        public SocketClient(Socket socket)
        {
            this.socket = socket;

            Initialise();
            StartReceiving();
        }

        private void Initialise()
        {
            recvBuffer = new byte[recvBufferLength];
            recvArgs = new SocketAsyncEventArgs();
            recvArgs.SetBuffer(recvBuffer, 0, recvBufferLength);
            recvArgs.Completed += DataReceived;

            sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += DataSent;
            toSendBuffers = new List<ArraySegment<byte>>();
            sendingBuffers = new List<ArraySegment<byte>>();
            sendBuffersLock = new SpinLock();

            disconnectionNotified = 0;
            isSending = 0;

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
                        HandleDisconnection(null);

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
            if (buffer.Array == null)
                throw new ArgumentNullException("buffer.Array");

            AddBufferToBufferList(buffer);

            if (Interlocked.CompareExchange(ref isSending, 1, 0) == 0)
            {
                StartSending();
            }
        }

        private void AddBufferToBufferList(ArraySegment<byte> buffer)
        {
            bool gotLock = false;
            sendBuffersLock.Enter(ref gotLock);
            if (!gotLock)
                throw new InvalidOperationException("Internal lock could not be acquired");
            try
            {
                toSendBuffers.Add(buffer);
            }
            finally
            {
                sendBuffersLock.Exit();
            }
        }

        private void StartSending()
        {
            bool gotLock = false;
                
            try
            {
                sendBuffersLock.Enter(ref gotLock);
                if (!gotLock)
                    throw new InvalidOperationException("Internal lock could not be acquired");

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
            }
            catch (Exception exc)
            {
                HandleDisconnection(exc);
            }
            finally
            {
                if (gotLock)
                    sendBuffersLock.Exit();
            }
        }

        private void DataSent(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError == SocketError.Success)
                {
                    int transferred = e.BytesTransferred;

                    if (transferred == 0)
                    {
                        //Graceful disconnection
                        HandleDisconnection(null);

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
            if (Interlocked.CompareExchange(ref disconnectionNotified, 1, 0) == 0)
                return;

            log.Info("Client disconnected. " + exc.Message);

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

        private void OnDataSent()
        {

        }
    }
}
