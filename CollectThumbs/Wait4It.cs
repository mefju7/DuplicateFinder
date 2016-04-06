using System;

namespace ThumbCollector
{
    internal class Wait4It
    {
        private static object lockObject = new object();
        private static int simultaneous = 0;
        public Wait4It()
        {
            lock (lockObject)
            {
                ++simultaneous;
            }
        }
        ~Wait4It()
        {
            lock (lockObject)
            {
                --simultaneous;
            }
        }

        public static bool Working {
            get {
                // Console.Out.WriteLine("have approx {0}", simultaneous);
                return simultaneous > 0;
            }
        }
    }
}