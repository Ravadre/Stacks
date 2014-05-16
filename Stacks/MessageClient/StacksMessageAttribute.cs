using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StacksMessageAttribute : Attribute
    {
        public int MessageId { get; private set; }

        public StacksMessageAttribute(int messageId)
        {
            MessageId = messageId;
        }
    }
}
