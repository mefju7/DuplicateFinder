using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThumbCollector
{
    class Program
    {
        static void Main(string[] args)
        {
            var ct = new CollectThumbnails();
            ct.run(args);
            Console.Out.WriteLine("good bye");
            Thread.Sleep(5000);
        }
    }
}
