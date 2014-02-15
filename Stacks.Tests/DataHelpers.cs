using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Tests
{
    public class DataHelpers
    {
        public static byte[] CreateRandomBuffer(int size)
        {
            var rng = RandomNumberGenerator.Create();
            var buffer = new byte[size];
            rng.GetBytes(buffer);

            return buffer;
        }
    }
}
