using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Discriminator

{
    class Program
    {
        static void Main(string[] args)
        {
            var fd = new FindDuplicates();
            fd.run();
            Console.Out.WriteLine("good bye");
            Thread.Sleep(5000);
        }
    }
}
