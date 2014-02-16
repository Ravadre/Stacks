using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public static class Ensure
    {
        public static void IsNotNull<T>(T o, string name) where T: class
        {
            if (o == null)
                throw new ArgumentNullException(name);
        }
    }
}
