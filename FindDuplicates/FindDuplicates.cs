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
        private const int MaxIter = 16;
        private const int MaxPicturesInDiscriminator = 32;
        private const double MinimalDiff = ChunkSize*3; // 
        private const int MaxDiff = 64;
        object myLock = new object(); // just for synchronizing
        object countLock = new object(); // for synchronizing counting
        HashSet<Tuple<int, int>> considered = new HashSet<Tuple<int, int>>();
        HashSet<Tuple<int, int>> matches = new HashSet<Tuple<int, int>>();
        private double totalMatches = 0;
        private long matchesDone = 0;
        private MemoryMappedViewAccessor acc;
        private object mmfLock = new object();

        public FindDuplicates()
        {
        }

        internal void run()
        {
            if (File.Exists(BinFile))
            {
                var fi = new FileInfo(BinFile);
                long cnt = fi.Length / ChunkSize;
                totalMatches = cnt * (cnt - 1) / 2;
                if (fi.Length != cnt * ChunkSize)
                {
                    Console.Error.WriteLine("Size of binfile is wrong");
                    return;
                }
                using (var mmf = MemoryMappedFile.CreateFromFile(BinFile))
                using (acc = mmf.CreateViewAccessor())
                {
                    var l = new List<int>();
                    for (int i = 0; i < cnt; ++i)
                        l.Add(i);
                    var t = Task.Run(() => analyze(l));
                    Console.Out.WriteLine("Let's start...");
                    Thread.Sleep(2000);
                    double per = 0;
                    var leftTime = new LeftTime();
                    while (Wait4It.Working)
                    {
                        per = matchesDone / totalMatches;
                        var finishAt = leftTime.get(totalMatches - matchesDone);
                        Console.WriteLine("divide {0:#,0} / {1:#,0} ({2:0%}) --> cand {3}, finished at {4}",
                            matchesDone, totalMatches, per, matches.Count, finishAt.ToString("MM-dd HH:mm:ss"));
                        Thread.Sleep(2000);
                    }
                }
                Console.Out.WriteLine("Combining");
                Dictionary<int, HashSet<int>> sets = new Dictionary<int, HashSet<int>>();
                foreach(var m in matches)
                {
                    var nhs = new HashSet<int>();
                    var items = new int[] { m.Item1, m.Item2 };
                    foreach(var it in items)
                    {
                        nhs.Add(it);
                        HashSet<int> oldOne;
                        if (sets.TryGetValue(it, out oldOne))
                        {
                            nhs.UnionWith(oldOne);
                        }
                    }
                    foreach(var it in nhs)
                    {
                        sets.Remove(it);
                        sets.Add(it, nhs);
                    }
                }
                List<int> keys = new List<int>();
                keys.AddRange(sets.Keys);
                List<HashSet<int>> allSets = new List<HashSet<int>>();
                int totalCount = 0;
                foreach(var it in keys)
                {
                    HashSet<int> foundSet;
                    if (sets.TryGetValue(it, out foundSet))
                    {
                        totalCount += foundSet.Count;
                        allSets.Add(foundSet);
                        foreach (var k in foundSet)
                            sets.Remove(k);
                    }
                }
                Console.WriteLine("found {0} duplicates", totalCount - allSets.Count);
                using (var setWriter = File.CreateText("sets.txt"))
                {
                    foreach(var set in allSets)
                    {
                        List<int> pics = new List<int>();
                        pics.AddRange(set);
                        setWriter.WriteLine("+{0}", pics[0]+1);
                        for (int i = 1; i < pics.Count; ++i)
                            setWriter.WriteLine("-{0}", pics[i] + 1);
                    }
                }
            }
        }



        private void analyze(List<int> picList)
        {
            long newLists = 0;
            try
            {
                var w4it = new Wait4It();
                var values = new double[picList.Count];
                double lowest, highest;
                getDiscrimatorValues(picList, values, out lowest, out highest);
                if (highest - lowest < MinimalDiff)
                {
                    Task.Run(() => match(picList));
                    var sz = picList.Count;
                    newLists += sz * (sz - 1) / 2;

                }
                else
                {
                    var highCut = (3 * highest + 2 * lowest) / 5;
                    var lowCut = (2 * highest + 3 * lowest) / 5;
                    var middle = (highest + lowest) / 2;
                    var lists = new List<int>[3];
                    for (int i = 0; i < lists.Length; ++i)
                        lists[i] = new List<int>();
                    for (int picNum = 0; picNum < values.Length; ++picNum)
                    {
                        var v = values[picNum];
                        var p = picList[picNum];
                        if (v < middle)
                        {
                            lists[0].Add(p);
                            if (v > lowCut)
                                lists[1].Add(p);
                        }
                        else
                        {
                            lists[2].Add(p);
                            if (v < highCut)
                                lists[1].Add(p);
                        }
                        
                    }

                    foreach (var l in lists)
                    {
                        var sz = l.Count;
                        if (sz <= 1)
                            continue; // no point in doing that.
                        newLists += sz * (sz - 1) / 2;
                        if (l.Count > 32)
                            Task.Run(() => analyze(l));
                        else
                            Task.Run(() => match(l));
                    }
                }
            }
            finally
            {
                long n = picList.Count;
                lock (countLock)
                {
                    n = n * (n - 1) / 2 - newLists;
                    matchesDone += n;
                }
            }
        }

        private void getDiscrimatorValues(List<int> picList, double[] values, out double lowest, out double highest)
        {


            var picBytes = new byte[ChunkSize];
            int r;
            lock (mmfLock)
            {
                r = acc.ReadArray(picList[0] * ChunkSize, picBytes, 0, ChunkSize);
            }

            var disc = new double[ChunkSize];
            double total = 0;
            for (int i = 0; i < ChunkSize; ++i)
                total += disc[i] = picBytes[i];
            if (total == 0)
            {
                Console.Error.WriteLine("black picture?");
            }
            lowest = highest = 0;
            var leftPic = new double[ChunkSize];
            var rightPic = new double[ChunkSize];
            long leftPictures = 1;
            long rightPictures = 1;
            for (int iter = 0; iter < MaxIter; ++iter)
            {
                int lastPicNum = picList.Count;
                if (iter < MaxIter - 1)
                {
                    // shorten the analysis
                    if (lastPicNum > MaxPicturesInDiscriminator)
                        lastPicNum = MaxPicturesInDiscriminator;
                }
                for (int picNum = 0; picNum < lastPicNum; ++picNum)
                {
                    var pic = picList[picNum];
                    total = 0;
                    lock (mmfLock)
                    {
                        r = acc.ReadArray(pic * ChunkSize, picBytes, 0, ChunkSize);
                    }

                    for (int i = 0; i < ChunkSize; ++i)
                        total += disc[i] * picBytes[i];
                    values[picNum] = total;
                }
                lowest = values[0];
                highest = values[0];
                for (int i = 1; i < lastPicNum; ++i)
                {
                    if (values[i] < lowest)
                        lowest = values[i];
                    if (values[i] > highest)
                        highest = values[i];
                }
                //if (highest - lowest < MinimalDiff)
                //    break;
                var discValue = (highest + lowest) / 2.0;
                if (iter < MaxIter - 1)
                {
                    for (int picNum = 0; picNum < lastPicNum; ++picNum)
                    {
                        var pic = picList[picNum];
                        lock (mmfLock)
                        {
                            r = acc.ReadArray(pic * ChunkSize, picBytes, 0, ChunkSize);
                        }

                        if (values[picNum] < discValue)
                        {
                            for (int i = 0; i < ChunkSize; ++i)
                                leftPic[i] += picBytes[i];
                            ++leftPictures;
                        }
                        else
                        {
                            for (int i = 0; i < ChunkSize; ++i)
                                rightPic[i] += picBytes[i];
                            ++rightPictures;
                        }
                    }
                    for (int i = 0; i < ChunkSize; ++i)
                        disc[i] = rightPic[i] / rightPictures - leftPic[i] / leftPictures;
                }
            }

        }


        private void match(List<int> l)
        {
            var w4it = new Wait4It();
            try
            {
                if (l.Count <= 1)
                    return;
                var picBytes = new byte[ChunkSize];
                var otherBytes = new byte[ChunkSize];
                for (int j = 1; j < l.Count; ++j)
                {
                    var pic2 = l[j];
                    lock (mmfLock)
                    {
                        acc.ReadArray(pic2 * ChunkSize, picBytes, 0, ChunkSize);
                    }
                    for (int i = 0; i < j; ++i)
                    {
                        var pic1 = l[i];
                        lock (considered)
                        {
                            var t = Tuple.Create(pic1, pic2);
                            var b = considered.Add(t);
                            if (!b) // have it already
                                continue;
                        }
                        lock (mmfLock)
                        {
                            acc.ReadArray(pic1 * ChunkSize, otherBytes, 0, ChunkSize);
                        }
                        bool noMatch = false;
                        int total = 0;
                        for (var k = 0; k < ChunkSize; ++k)
                        {
                            int d = otherBytes[k] - picBytes[k];
                            total += d;
                            if (Math.Abs(d) > MaxDiff)
                            {
                                noMatch = true;
                                break;
                            }
                        }
                        if (noMatch)
                            continue;
                            lock (matches)
                            {
                                var t = Tuple.Create(pic1, pic2);
                                matches.Add(t);
                            }
                    }
                }
            }
            finally
            {
                lock (countLock)
                {
                    matchesDone += l.Count * (l.Count - 1) / 2;
                }
            }

        }



    }
}