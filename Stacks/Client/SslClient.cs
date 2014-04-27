using Stacks.Executors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public class SslClient : ISocketClient
    {
        public event Action<ArraySegment<byte>> Received;
        public event Action<int> Sent;
        public event Action<Exception> Disconnected;
        public event Action Connected;

        private ISocketClient client;
        private SslStream sslStream;
        private RawByteClientStream clientStreamWrapper;
        private string targetHost;
        private bool isClient;
        private X509Certificate serverCertificate;

        private bool disconnectCalled;

        private const int internalBufferLength = 4096;

        public IExecutor Executor { get { return this.client.Executor; } }
        public bool IsConnected { get { return this.client.IsConnected; } }
        
        /// <summary>
        /// Initialises ssl client as a client side endpoint.
        /// </summary>
        public SslClient(ISocketClient client, string targetHost)
            : this(client, targetHost, allowEveryCertificate: false)
        { }

        /// <summary>
        /// Initialises ssl client as a client side endpoint.
        /// </summary>
        public SslClient(ISocketClient client, string targetHost, bool allowEveryCertificate)
        {
            if (allowEveryCertificate)
                InitializeAsClient(client, targetHost, AllowEveryCertificate);
            else
                InitializeAsClient(client, targetHost, null);
        }

        /// <summary>
        /// Initialises ssl client as a client side endpoint.
        /// </summary>
        public SslClient(ISocketClient client,
                         string targetHost,
                         RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            InitializeAsClient(client, targetHost, remoteCertificateValidationCallback);
        }

        /// <summary>
        /// Initialises ssl client as a server side endpoint.
        /// It is assumed, that passed client is already connected.
        /// EstablishSsl should be called when this constructor is used.
        /// </summary>
        public SslClient(ISocketClient client,
                         X509Certificate serverCertificate)
        {
            Ensure.IsNotNull(client, "client");
            Ensure.IsNotNull(serverCertificate, "serverCertificate");

            if (!client.IsConnected)
                throw new InvalidOperationException("Socket client should be connected");

            InitializeAsServer(client, serverCertificate);
        }

        private void InitializeAsServer(ISocketClient client,
                                        X509Certificate certificate)
        {
            InitializeCommon(client, isClient: false);

            this.serverCertificate = certificate;
            this.clientStreamWrapper = new RawByteClientStream(this.client);
            this.sslStream = new SslStream(
                this.clientStreamWrapper,
                true);
        }

        private void InitializeAsClient(ISocketClient client,
                                        string targetHost,
                                        RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            InitializeCommon(client, isClient: true);

            this.targetHost = targetHost;
            this.clientStreamWrapper = new RawByteClientStream(this.client);
            this.sslStream = new SslStream(
                this.clientStreamWrapper,
                true,
                remoteCertificateValidationCallback,
                null,
                EncryptionPolicy.RequireEncryption);
        }

        private void InitializeCommon(ISocketClient client,
                                bool isClient)
        {
            this.isClient = isClient;
            this.disconnectCalled = false;
            this.client = client;

            this.client.Disconnected += ClientDisconnected;
            this.client.Connected += ClientConnected;
            this.client.Sent += ClientSentData;
        }

        private void ClientSentData(int count)
        {
            OnSent(count);
        }

        /// <summary>
        /// Connectes to a remote host. Connected event is raised when connection
        /// and ssl stream are both established.
        /// </summary>
        /// <param name="remoteEndPoint"></param>
        public void Connect(IPEndPoint remoteEndPoint)
        {
            this.client.Connect(remoteEndPoint);
        }

        /// <summary>
        /// This method should be called only if SslClient was created 
        /// with already connected socket. In this case, this method will
        /// establish ssl and call Connected event.
        /// </summary>
        public void EstablishSsl()
        {
            this.Executor.Enqueue(() => ClientConnected());
        }

        private async void ClientConnected()
        {
            try
            {
                if (this.isClient)
                {
                    await this.sslStream.AuthenticateAsClientAsync(this.targetHost);
                }
                else
                {
                    await this.sslStream.AuthenticateAsServerAsync(this.serverCertificate);
                }

                //Rest of the code is executed using executor.

                ReadLoop();
                OnConnected();
            }
            catch (Exception exn)
            {
                HandleSslDisconnection(exn);
            }
        }

        private void HandleSslDisconnection(Exception exn)
        {
            this.disconnectCalled = true;
            OnDisconnected(exn);

            CleanupStreams();
            try
            {
                this.client.Close();
            }
            catch { }
        }

        private void ClientDisconnected(Exception exn)
        {
            if (this.disconnectCalled)
                return;
            this.disconnectCalled = true;

            CleanupStreams();
            OnDisconnected(exn);
        }

        private void CleanupStreams()
        {
            try
            {
                this.clientStreamWrapper.Dispose();
            }
            catch { }

            try
            {
                this.sslStream.Dispose();
            }
            catch { }
        }

        private async void ReadLoop()
        {
            //This code is executed on executor automatically
            try
            {
                var buf = new byte[internalBufferLength];

                while (true)
                {
                    var read = await this.sslStream.ReadAsync(buf, 0, internalBufferLength);

                    if (read == 0)
                        break;

                    OnReceived(new ArraySegment<byte>(buf, 0, read));
                }
            }
            catch
            {

            }
        }

        //TODO: Finish this
        //private X509Certificate UserCertificateSelectionCallback(
        //                object sender, 
        //                string targetHost, 
        //                X509CertificateCollection localCertificates, 
        //                X509Certificate remoteCertificate, 
        //                string[] acceptableIssuers)
        //{
        //    throw new NotImplementedException();
        //}

        public void Send(byte[] buffer)
        {
            this.sslStream.Write(buffer);
        }

        public void Send(ArraySegment<byte> buffer)
        {
            this.sslStream.Write(buffer.Array, buffer.Offset, buffer.Count);
        }

        public void Close()
        {
            this.client.Close();
        }

        private bool AllowEveryCertificate(
                        object sender,
                        X509Certificate certificate,
                        X509Chain chain,
                        SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }



        private void OnReceived(ArraySegment<byte> data)
        {
            var handler = this.Received;

            if (handler != null)
            {
                try { handler(data); }
                catch { }
            }
        }

        private void OnConnected()
        {
            var handler = this.Connected;

            if (handler != null)
            {
                try { handler(); }
                catch { }
            }
        }

        private void OnDisconnected(Exception exn)
        {
            var handler = this.Disconnected;

            if (handler != null)
            {
                try { handler(exn); }
                catch { }
            }
        }

        private void OnSent(int count)
        {
            var handler = this.Sent;

            if (handler != null)
            {
                try { handler(count); }
                catch { }
            }
        }
    }

    public class RawByteClientStream : Stream
    {
        private IRawByteClient client;
        ResizableCyclicBuffer buffer;
        private ManualResetEventSlim hasDataEvent;
        private bool disposed;

        public RawByteClientStream(IRawByteClient client)
        {
            this.disposed = false;
            this.hasDataEvent = new ManualResetEventSlim();
            this.client = client;
            this.buffer = new ResizableCyclicBuffer(4096);
            this.client.Received += DataReceived;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {

        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.hasDataEvent.Wait();

            if (this.disposed)
                throw new ObjectDisposedException("Stream");

            lock (this.buffer)
            {
                var segment = new ArraySegment<byte>(buffer, offset, count);
                var read = this.buffer.ReadRawBytes(segment);

                if (this.buffer.Count == 0)
                    this.hasDataEvent.Reset();

                return read;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            client.Send(new ArraySegment<byte>(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        private void DataReceived(ArraySegment<byte> data)
        {
            lock (this.buffer)
            {
                this.buffer.AddData(data);
                this.hasDataEvent.Set();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            try
            {
                disposed = true;
                this.hasDataEvent.Set();
            }
            catch { }
        }

    }
}
