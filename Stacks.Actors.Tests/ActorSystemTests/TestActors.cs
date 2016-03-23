using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stacks.Actors;

namespace Stacks.Tests.ActorSystemTests
{

    public interface ICalculatorActor : IActor
    {
        Task<double> Div(double x, double y);
    }

    public interface ICalculatorExActor : IActor
    {
        Task<double> Div(double x, double y);
        Task<double> AddThenStop(double x, double y);
        Task<double> Throw(string msg);
        Task<double> Complicated(double x, double y);
        Task<double> ComplicatedThenThrow(double x, double y);
        Task NoOp();
    }

    public interface ISingleMethodActor
    {
        Task<int> Test();
    }

    public interface IExplicitInterfaceActor
    {
        Task<double> Sum(double[] xs);
        IObservable<double> Counter { get; }
    }

    public class ExplicitInterfaceActor : Actor, IExplicitInterfaceActor
    {
        IObservable<double> IExplicitInterfaceActor.Counter
        {
            get { return Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)).Select(l => (double)l); }
        }

        async Task<double> IExplicitInterfaceActor.Sum(double[] xs)
        {
            await Context;
            return xs.Sum();
        }
    }

    public class SingleMethodActor : Actor, ISingleMethodActor
    {
        public async Task<int> Test()
        {
            await Context;

            return 5;
        }
    }

    public class CalculatorActor : Actor, ICalculatorActor
    {
        public async Task<double> Div(double x, double y)
        {
            await Context;

            await Task.Delay(50);

            if (y == 0.0)
                throw new DivideByZeroException();

            return x / y;
        }
    }

    public class ActorWithExtraMethod : Actor, ICalculatorActor
    {
        public async Task<double> Div(double x, double y)
        {
            await Context;
            return 5.0 + TestMethod();
        }

        public int TestMethod()
        {
            return 10;
        }
    }

    public class LongStopActor : Actor, ICalculatorActor
    {
        public async Task<double> Div(double x, double y)
        {
            await Context;
            return 5;
        }

        protected override void OnStopped()
        {
            Thread.Sleep(1000);
        }
    }
}