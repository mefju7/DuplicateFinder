using System.Threading;

namespace ThumbCollector
{
    internal class Wait4It
    {
        private Semaphore sem;

        public Wait4It(Semaphore sem)
        {
            this.sem = sem;
            sem.WaitOne();
        }
        ~Wait4It()
        {
            sem.Release();
        }
       
    }
}