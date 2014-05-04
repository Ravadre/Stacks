using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtobufSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Server s = new Server();
            Client c = new Client();
            s.Run();
            c.Run(s.ServerPort);

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
