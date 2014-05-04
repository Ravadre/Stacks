using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks;
using Stacks.Actors;

namespace ActorSample1
{
    class Program
    {
        static void Main(string[] args)
        {
            var formatter = new Formatter();
            var helloPrinter = new Hello(formatter);

            helloPrinter.SayHelloToFriends(new[] { "Stan", "Scott", "John" });
        }
    }

    class Hello
    {
        private Formatter formatter;

        public Hello(Formatter formatter)
        {
            this.formatter = formatter;
        }

        public void SayHelloToFriends(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                Console.WriteLine(formatter.SayHello(name).Result);
            }
        }
    }

    //One of the ways of defining an actor is to 
    //inherit from Actor class
    class Formatter : Actor
    {
        //Because every call has to be scheduled on
        //actor's context, answer will not be ready instantly,
        //so Task<T> is returned.
        public async Task<string> SayHello(string name)
        {
            //Code after this await will be run
            //on actor's context.
            await Context;

            //When an actor wants to reply to request, it just
            //has to return the response.
            return "Hello " + name + "!";
        }
    }
}
