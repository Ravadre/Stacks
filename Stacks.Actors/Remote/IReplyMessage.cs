using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public interface IReplyMessage<T>
    {
        T GetResult();
        void SetResult(T result);
        void SetError(string error);
    }
}
