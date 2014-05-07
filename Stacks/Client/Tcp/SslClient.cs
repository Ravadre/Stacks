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

namespace Stacks.Tcp
{
    public class SslClient : IRawByteClient
    {
        public event Action<ArraySegment<byte>> Received;
        public event Action<int> Sent;
        public event Action<Exception> Disconnected;
        public event Action Connected;

        private TaskCompletionSource<int> connectedTcs;

        private IRawByteClient client;
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
        public SslClient(IRawByteClient client, string targetHost)
            : this(client, targetHost, allowEveryCertificate: false)
        { }

        /// <summary>
        /// Initialises ssl client as a client side endpoint.
        /// </summary>
        public SslClient(IRawByteClient client, string targetHost, bool allowEveryCertificate)
        {
            Ensure.IsNotNull(client, "client");
            Ensure.IsNotNullOrWhiteSpace(targetHost, "targetHost");

            if (allowEveryCertificate)
                InitializeAsClient(client, targetHost, AllowEveryCertificate);
            else
                InitializeAsClient(client, targetHost, null);
        }

        /// <summary>
        /// Initialises ssl client as a client side endpoint.
        /// </summary>
        public SslClient(IRawByteClient client,
                         string targetHost,
                         RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            Ensure.IsNotNull(client, "client");
            Ensure.IsNotNullOrWhiteSpace(targetHost, "targetHost");

            InitializeAsClient(client, targetHost, remoteCertificateValidationCallback);
        }

        /// <summary>
        /// Initialises ssl client as a server side endpoint.
        /// It is assumed, that passed client is already connected.
        /// EstablishSsl should be called when this constructor is used.
        /// </summary>
        public SslClient(IRawByteClient client,
                         X509Certificate serverCertificate)
        {
            Ensure.IsNotNull(client, "client");
            Ensure.IsNotNull(serverCertificate, "serverCertificate");

            if (!client.IsConnected)
                throw new InvalidOperationException("Socket client should be connected");

            InitializeAsServer(client, serverCertificate);
        }

        private void InitializeAsServer(IRawByteClient client,
                                        X509Certificate certificate)
        {
            InitializeCommon(client, isClient: false);

            this.serverCertificate = certificate;
            this.clientStreamWrapper = new RawByteClientStream(this.client);
            this.sslStream = new SslStream(
                this.clientStreamWrapper,
                true);
        }

        private void InitializeAsClient(IRawByteClient client,
                                        string targetHost,
                                        RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            InitializeCommon(client, isClient: true);

            this.targetHost = targetHost;
            this.clientStreamWrapper = new RawByteClientStream(this.client);
#if !MONO
            this.sslStream = new SslStream(
                this.clientStreamWrapper,
                true,
                remoteCertificateValidationCallback,
                null,
                EncryptionPolicy.RequireEncryption);
#else
            this.sslStream = new SslStream(
                this.clientStreamWrapper,
                true,
                remoteCertificateValidationCallback,
                null);
#endif
        }

        private void InitializeCommon(IRawByteClient client,
                                bool isClient)
        {
            this.connectedTcs = new TaskCompletionSource<int>();
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
        public Task Connect(IPEndPoint remoteEndPoint)
        {
            Ensure.IsNotNull(remoteEndPoint, "remoteEndPoint");

            this.client.Connect(remoteEndPoint);

            return connectedTcs.Task;
        }

        /// <summary>
        /// This method should be called only if SslClient was created 
        /// with already connected socket. In this case, this method will
        /// establish ssl and call Connected event.
        /// </summary>
        public Task EstablishSsl()
        {
            this.Executor.Enqueue(() => ClientConnected());

            return connectedTcs.Task;
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
            Ensure.IsNotNull(buffer, "buffer");

            this.sslStream.Write(buffer);
        }

        public void Send(ArraySegment<byte> buffer)
        {
            Ensure.IsNotNull(buffer.Array, "buffer.Array");

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
            NotifyConnectedTask(null);

            var handler = this.Connected;

            if (handler != null)
            {
                try { handler(); }
                catch { }
            }
        }

        private void OnDisconnected(Exception exn)
        {
            NotifyConnectedTask(exn);

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

        private void NotifyConnectedTask(Exception error)
        {
            if (error == null)
                connectedTcs.SetResult(0);
            else
                connectedTcs.SetException(error);
        }
    }
}
