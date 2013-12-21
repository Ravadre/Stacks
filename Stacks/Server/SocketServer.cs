using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;
using Stacks.Executors;

namespace Stacks.Server
{
    public class SocketServer
    {
        private Socket socket;
        private SocketAsyncEventArgs acceptArgs;

        private IPEndPoint bindEndPoint;

        public IPEndPoint BindEndPoint { get { return bindEndPoint; } }

        public event Action Started;
        public event Action Stopped;
        public event Action<Socket> Connected;

        private IExecutor executor;

        private int hasStarted;

        public SocketServer(IExecutor executor, IPEndPoint bindEndPoint)
        {
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
            this.bindEndPoint = (IPEndPoint)this.socket.LocalEndPoint;
            this.socket.Listen(10);
            
            OnStarted();

            StartAccepting();
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
                            Console.WriteLine("Server stopped");
                            OnStopped();
                            break;
                        }
                    case SocketError.Success:
                        {
                            OnConnected(e.AcceptSocket);
                            StartAccepting();
                            break;
                        }
                    case SocketError.ConnectionReset:
                        {
                            Console.WriteLine("Potential half-open SYN scan occured");
                            StartAccepting();
                            break;
                        }
                    default:
                        {
                            OnStopped();
                            break;
                        }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine("Exception occured in SocketAccepted. Exc: " + exc);
                OnStopped();
            }
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

        private void OnConnected(Socket client)
        {
            var h = Connected;
            if (h != null)
            {
                executor.Enqueue(() => h(client));
            }
        }
    }
}
