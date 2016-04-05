using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace Discriminator
{
    internal class FindDuplicates
    {
        private const int ChunkSize = 3 * 16 * 16;
        private const string BinFile = "files.bin";
        ConcurrentBag<Task> allTasks = new ConcurrentBag<Task>();
        object myLock = new object(); // just for synchronizing
        private MemoryMappedViewAccessor acc;
        private Random rnd;

        public FindDuplicates()
        {
        }

        internal void run()
        {
            rnd = new Random();
            if (File.Exists(BinFile))
            {
                var fi = new FileInfo(BinFile);
                long cnt = fi.Length / ChunkSize;
                if (fi.Length != cnt * ChunkSize)
                {
                    Console.Error.WriteLine("Size of binfile is wrong");
                    return;
                }
                using (var mmf = MemoryMappedFile.CreateFromFile(BinFile))
                using (acc = mmf.CreateViewAccessor())
                {
                    var l = new List<long>();
                    for (long i = 0; i < cnt; ++i)
                        l.Add(i);
                    var t = Task.Run(() => analyze(l));
                    allTasks.Add(t);

                    Task tw4;
                    while (allTasks.TryTake(out tw4))
                    {
                        Console.Out.WriteLine("waiting for task");
                        tw4.Wait();
                    }
                }
            }
        }

        private void analyze(List<long> picList)
        {
            // the discriminator
            var disc = new double[ChunkSize];
            for (int i = 0; i < ChunkSize; ++i)
                disc[i] = rnd.NextDouble() - 0.5;
            var leftPic = new double[ChunkSize];
            long leftPictures;
            long rightPictures;
            double middleValue = 0;
            double lowest, highest;
            var rightPic = new double[ChunkSize];
            var picBytes = new byte[ChunkSize];
            for (int iter = 0; iter < 100; ++iter)
            {
                leftPictures = rightPictures = 1;
                highest = Double.NegativeInfinity;
                lowest = Double.PositiveInfinity;
                for (int i = 0; i < ChunkSize; ++i)
                    leftPic[i] = rightPic[i] = 0;
                int p = 0;
                foreach (var pic in picList)
                {
                    double total = 0;
                    if (++p > 500)
                        break;
                    acc.ReadArray(pic * ChunkSize, picBytes, 0, ChunkSize);
                    for (int i = 0; i < ChunkSize; ++i)
                        total = disc[i] * picBytes[i];
                    if (total > middleValue)
                    {
                        for (int i = 0; i < ChunkSize; ++i)
                            rightPic[i] += picBytes[i];
                        ++rightPictures;
                    }
                    else
                    {
                        for (int i = 0; i < ChunkSize; ++i)
                            leftPic[i] += picBytes[i];
                        ++leftPictures;
                    }
                    if (total > highest) highest = total;
                    if (total < lowest) lowest = total;
                }
                for (int i = 0; i < ChunkSize; ++i)
                    disc[i] = rightPic[i] / rightPictures - leftPic[i] / leftPictures;
                middleValue = (highest + lowest) / 2.0;
            }
            // now dividing the list into three

        }
    }
}