using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;

namespace Stacks.Tcp
{
    public class SocketServer
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        private Socket socket;
        private SocketAsyncEventArgs acceptArgs;

        private IPEndPoint bindEndPoint;

        public IPEndPoint BindEndPoint { get { return bindEndPoint; } }

        public event Action Started;
        public event Action Stopped;
        public event Action<SocketClient> Connected;

        private IExecutor executor;

        private int hasStarted;

        public SocketServer(IPEndPoint bindEndPoint)
            : this(new ActionBlockExecutor(), bindEndPoint)
        { }

        public SocketServer(IExecutor executor, IPEndPoint bindEndPoint)
        {
            Ensure.IsNotNull(executor, "executor");
            Ensure.IsNotNull(bindEndPoint, "bindEndPoint");

            this.executor = executor;

            this.socket = new Socket(AddressFamily.InterNetwork,
                                     SocketType.Stream,
                                     ProtocolType.Tcp);
            this.acceptArgs = new SocketAsyncEventArgs();
            this.bindEndPoint = bindEndPoint;

            this.acceptArgs.Completed += SocketAccepted;
        }

        public void Start()
        {
            VerifyFirstStart();

            this.socket.Bind(this.bindEndPoint);
            this.socket.LingerState = new LingerOption(false, 0);
            this.bindEndPoint = (IPEndPoint)this.socket.LocalEndPoint;
            this.socket.Listen(10);

            StartAccepting();
            OnStarted();
        }

        public void Stop()
        {
            this.socket.Close(0);
        }

        private void VerifyFirstStart()
        {
            if (Interlocked.CompareExchange(ref this.hasStarted, 1, 0) != 0)
            {
                throw new InvalidOperationException("Server already started");
            }
        }

        private void StartAccepting()
        {
            this.acceptArgs.AcceptSocket = null;
            bool isPending = this.socket.AcceptAsync(this.acceptArgs);
            if (!isPending)
                SocketAccepted(this, this.acceptArgs);
        }

        private void SocketAccepted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                switch (e.SocketError)
                {
                    case SocketError.ConnectionAborted:
                        {
                            log.Info("Server stopped");
                            OnStopped();
                            break;
                        }
                    case SocketError.Success:
                        {
                            var sc = CreateSocketClient(e.AcceptSocket);
                            OnConnected(sc);

                            StartAccepting();
                            break;
                        }
                    case SocketError.ConnectionReset:
                        {
                            log.Warn("Potential half-open SYN scan occured");
                            StartAccepting();
                            break;
                        }
                    default:
                        {
                            log.Error("Error occured while connecting new client: " + e.SocketError);
                            StartAccepting();
                            break;
                        }
                }
            }
            catch (Exception exc)
            {
                log.Error("Exception occured in SocketAccepted. Exc: " + exc);
                OnStopped();
            }
        }

        private SocketClient CreateSocketClient(Socket socket)
        {
            return new SocketClient(this.executor, socket);
        }

        private void OnStarted()
        {
            var h = Started;
            if (h != null)
            {
                executor.Enqueue(h);
            }
        }

        private void OnStopped()
        {
            var h = Stopped;
            if (h != null)
            {
                executor.Enqueue(h);
            }
        }

        private void OnConnected(SocketClient client)
        {
            var h = Connected;
            if (h != null)
            {
                executor.Enqueue(() => h(client));
            }
        }
    }
}
