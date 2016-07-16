using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Stacks;
using System.Net;

namespace Stacks.Tests
{
    public class IPHelpersTests
    {
        [Fact]
        public void Localhost_should_return_loopback_address()
        {
            var ep = IPHelpers.Parse("tcp://localhost:1234").Result;

            Assert.Equal(IPAddress.Loopback, ep.Address);
            Assert.Equal(1234, ep.Port);
        }

        [Fact]
        public void IPv6_Localhost_should_return_loopback_address()
        {
            var ep = IPHelpers.Parse("tcp6://localhost:1234").Result;

            Assert.Equal(IPAddress.IPv6Loopback, ep.Address);
            Assert.Equal(1234, ep.Port);
        }

        [Fact]
        public void Numeric_ip_should_be_parsed_properly()
        {
            var ep = IPHelpers.Parse("tcp://10.43.12.43:1234").Result;

            Assert.Equal(IPAddress.Parse("10.43.12.43"), ep.Address);
            Assert.Equal(1234, ep.Port);
        }

        [Fact]
        public void Asterisk_should_be_parsed_as_any()
        {
            var ep = IPHelpers.Parse("tcp://*:1234").Result;

            Assert.Equal(IPAddress.Any, ep.Address);
            Assert.Equal(1234, ep.Port);
        }

        [Fact]
        public void Plus_should_be_parsed_as_any()
        {
            var ep = IPHelpers.Parse("tcp://+:1234").Result;

            Assert.Equal(IPAddress.Any, ep.Address);
            Assert.Equal(1234, ep.Port);
        }

        [Fact]
        public void IPv6_Asterisk_should_be_parsed_as_any()
        {
            var ep = IPHelpers.Parse("tcp6://*:1234").Result;

            Assert.Equal(IPAddress.IPv6Any, ep.Address);
            Assert.Equal(1234, ep.Port);
        }

        [Fact]
        public void IPv6_Plus_should_be_parsed_as_any()
        {
            var ep = IPHelpers.Parse("tcp6://+:1234").Result;

            Assert.Equal(IPAddress.IPv6Any, ep.Address);
            Assert.Equal(1234, ep.Port);
        }

        [Fact]
        public async Task IP_should_resolve_from_host()
        {
            var ep = IPHelpers.Parse("tcp://google.com:1234").Result;

            Assert.Equal(1234, ep.Port);
        }
    }
}
