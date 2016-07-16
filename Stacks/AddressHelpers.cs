using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Stacks
{
    public static class IPHelpers
    {
        private static Regex protoRegex = new Regex(@"(?<proto>.+)\://(?<address>([^:]+))\:(?<port>\d{1,5})(?<path>/.*)?", 
                                                    RegexOptions.Compiled);

        public static async Task<IPEndPoint> Parse(string address)
        {
            Ensure.IsNotNull(address, "address");

            var match = protoRegex.Match(address.ToLowerInvariant());

            if (!match.Success)
                throw new ArgumentException("given address is not in valid format.", "address");

            var proto = match.Groups["proto"].Value;
            var addressOrHost = match.Groups["address"].Value;
            var port = int.Parse(match.Groups["port"].Value);

            bool isAny = false;
            bool isloop = false;

            if (addressOrHost == "+" || addressOrHost == "*")
                isAny = true;
            if (addressOrHost == "localhost")
                isloop = true;
            
            IPEndPoint ep;

            if (proto == "tcp")
            {
                if (isAny)
                    ep = new IPEndPoint(IPAddress.Any, port);
                else if (isloop)
                    ep = new IPEndPoint(IPAddress.Loopback, port);
                else
                {
                    IPAddress a;
                    if (IPAddress.TryParse(addressOrHost, out a))
                    {
                        ep = new IPEndPoint(a, port);
                    }
                    else
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(addressOrHost);
                        a = hostEntry.AddressList.FirstOrDefault();

                        if (a == null)
                            throw new Exception("Unrecognized host: " + addressOrHost);
                        ep = new IPEndPoint(a, port);
                    }
                }
                    

            }
            else if (proto == "tcp6")
            {
                if (isAny)
                    ep = new IPEndPoint(IPAddress.IPv6Any, port);
                else if (isloop)
                    ep = new IPEndPoint(IPAddress.IPv6Loopback, port);
                else
                {
                    IPAddress a;
                    if (IPAddress.TryParse(addressOrHost, out a))
                    {
                        ep = new IPEndPoint(a, port);
                    }
                    else
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(addressOrHost);
                        a = hostEntry.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetworkV6);

                        if (a == null)
                        {
                            if (hostEntry.AddressList.Length == 0)
                            {
                                throw new Exception("Unrecognized host: " + addressOrHost);
                            }
                            else
                            {
                                throw new Exception("Host " + addressOrHost + " has no IP6 address");
                            }
                        }
                            
                        ep = new IPEndPoint(a, port);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Only 'tcp' and 'tcp6' protocols are supported.");
            }

            return ep;
        }
    }
}
