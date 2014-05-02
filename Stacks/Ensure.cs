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

        public static void IsNotNullOrEmpty(string s, string name)
        {
            if (string.IsNullOrEmpty(s))
            {
                if (s == null)
                    throw new ArgumentNullException(name);
                else
                    throw new ArgumentException("Argument can't be empty", name);
            }
        }

        public static void IsNotNullOrWhiteSpace(string s, string name)
        {
            IsNotNullOrEmpty(s, name);

            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Argument can't consist of white spaces only", name);
        }
    }
}
