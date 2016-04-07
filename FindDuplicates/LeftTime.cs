using System;

namespace Discriminator
{
    internal class LeftTime
    {
        private static int BufLen = 16;
        private int idx = 0;
        private DateTime[] times;
        private double[] workLeft;

        public LeftTime()
        {
            this.times = new DateTime[BufLen];
            this.workLeft = new double[BufLen];
            var t = DateTime.Now;
            for (int i = 0; i < BufLen; ++i)
            {
                times[i] = t;
                workLeft[i] = Double.PositiveInfinity;
            }
        }

        internal DateTime get(double v)
        {
            var t = DateTime.Now;
            try
            {
                idx = (idx + 1) % BufLen;
                var done = workLeft[idx] - v;
                var ts = t.Subtract(times[idx]).TotalMilliseconds;
                if (done > 0)
                    return t.AddMilliseconds(ts * v / done);
                else
                    return t;
            }
            finally
            {
                times[idx] = t;
                workLeft[idx] = v;
            }
        }
    }
}