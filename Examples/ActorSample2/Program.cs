using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using System.Net.Http;
using System.Xml.Linq;

namespace ActorSample2
{
    class Program
    {
        static void Main(string[] args)
        {
            var weather = new Weather();

            try
            {
                var temp = weather.GetTemperature("Warsaw").Result;
                Console.WriteLine("Temperature in Warsaw, Poland: " + 
                                  temp.ToString("F2") + 
                                  "\u00B0C");
            }
            catch (Exception)
            {
                Console.WriteLine("Could not get temperature for Warsaw, Poland");
            }
        }
    }

    class Weather
    {
        //Actor can also be implemented by defining context
        private ActorContext actor = new ActorContext();
        private HttpClient httpClient = new HttpClient();

        public async Task<double> GetTemperature(string city)
        {
            await actor;

            //Other actors or services can be awaited, execution will be resumed
            //on actor context automatically.
            var data = await httpClient.GetStringAsync(
                string.Format("http://api.openweathermap.org/data/2.5/" + 
                              "weather?q={0}&mode=xml&units=metric",
                    city));

            return (double)XDocument.Parse(data)
                                    .Element("current")
                                    .Element("temperature")
                                    .Attribute("value");
        }
    }
}
