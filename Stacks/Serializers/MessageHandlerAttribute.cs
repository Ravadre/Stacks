using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MessageHandlerAttribute : Attribute
    {
        public int TypeCode { get; private set; }

        public MessageHandlerAttribute(int typeCode)
        {
            TypeCode = typeCode;
        }
    }
}
