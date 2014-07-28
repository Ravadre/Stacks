using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Stacks
{
    public static class IPHelpers
    {
        private static Regex protoRegex = new Regex(@"(?<proto>.+)\://(?<address>([\d+*.]+)|(localhost))(\:(?<port>\d{1,5}))(?<path>/.*)?", 
                                                    RegexOptions.Compiled);

        public static IPEndPoint Parse(string address)
        {
            Ensure.IsNotNull(address, "address");

            var match = protoRegex.Match(address.ToLowerInvariant());

            if (!match.Success)
                throw new ArgumentException("given address is not in valid format.", "address");

            var proto = match.Groups["proto"].Value;
            var ipAddress = match.Groups["address"].Value;
            var port = int.Parse(match.Groups["port"].Value);

            bool isAny = false;
            bool isloop = false;

            if (ipAddress == "+" || ipAddress == "*")
                isAny = true;
            if (ipAddress == "localhost")
                isloop = true;
            
            IPEndPoint ep;

            if (proto == "tcp")
            {
                if (isAny)
                    ep = new IPEndPoint(IPAddress.Any, port);
                else if (isloop)
                    ep = new IPEndPoint(IPAddress.Loopback, port);
                else
                    ep = new IPEndPoint(IPAddress.Parse(ipAddress), port);

            }
            else if (proto == "tcp6")
            {
                if (isAny)
                    ep = new IPEndPoint(IPAddress.IPv6Any, port);
                else if (isloop)
                    ep = new IPEndPoint(IPAddress.IPv6Loopback, port);
                else
                    ep = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            }
            else
            {
                throw new InvalidOperationException("Only 'tcp' and 'tcp6' protocols are supported.");
            }

            return ep;
        }
    }
}
