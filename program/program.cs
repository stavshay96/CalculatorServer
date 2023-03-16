using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Net;
using System.IO;
using CalculatorServer;

namespace program
{
    public class program
    {
        public static void Main(string[] args)
        {
            CalculatorController calculator = new CalculatorController();
            Console.WriteLine("Listening on port 9583...");

            while (true)
            {
                calculator.RunCalculator();
            }
        }
    }
}
