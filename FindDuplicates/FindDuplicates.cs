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
        private const string FilesFile = "files.txt";
        private const string SetFile = "sets-{0}.txt";
        private const int MaxIter = 16;
        private const int MaximalSetSize=256;
        private const int MaxPicturesInDiscriminator = 32;
        private const double MinimalDiff = ChunkSize * 3; // 
        private const int MaxDiff = 64;
        object myLock = new object(); // just for synchronizing
        object countLock = new object(); // for synchronizing counting
        HashSet<Tuple<int, int>> considered = new HashSet<Tuple<int, int>>();

        HashSet<Tuple<int, int>>[] matches;
        private double totalMatches = 0;
        private long matchesDone = 0;
        private MemoryMappedViewAccessor acc;
        private object mmfLock = new object();
        private int[] shiftPos;
        private int[] diffCounts = new int[256];

        public FindDuplicates()
        {
            shiftPos = new int[24];
            int p = 0;
            int r = 2;
            for (int i = -r; i <= r; ++i)
                for (int j = -r; j <= r; ++j)
                {
                    int k = (i * 16 + j) * 3;
                    if (k == 0)
                        continue;
                    shiftPos[p++] = k;
                }
            matches = new HashSet<Tuple<int, int>>[MaxDiff];
            for (int i = 0; i < matches.Length; ++i)
                matches[i] = new HashSet<Tuple<int, int>>();

        }

        internal void run()
        {
            Console.Out.WriteLine("reading and storing data in {0}", Directory.GetCurrentDirectory());
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
                        Console.WriteLine("divide {0:#,0} / {1:#,0} ({2:0%}) --> checked {3}, finished at {4}",
                            matchesDone, totalMatches, per, considered.Count, finishAt.ToString("MM-dd HH:mm:ss"));
                        Thread.Sleep(2000);
                    }
                }
                /* for(int i=0;i<diffCounts.Length;++i)
                {
                    Console.Out.WriteLine("diff {0} {1}", i, diffCounts[i]);
                }
                */
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Combining");
                // this dictionary is kind of a reverse lookup, which set contains the items
                Dictionary<int, HashSet<int>> sets = new Dictionary<int, HashSet<int>>();
                var fileNames = File.ReadAllLines(FilesFile);
                int maxSetSize = 0;
                for (int binDiff = 0; binDiff < matches.Length; ++binDiff)
                {
                    foreach (var m in matches[binDiff])
                    {
                        var nhs = new HashSet<int>();
                        var items = new int[] { m.Item1, m.Item2 };
                        foreach (var it in items)
                        {
                            nhs.Add(it);
                            HashSet<int> oldOne;
                            if (sets.TryGetValue(it, out oldOne))
                            {
                                nhs.UnionWith(oldOne);
                            }
                        }
                        foreach (var it in nhs)
                        {
                            sets.Remove(it);
                            sets.Add(it, nhs);
                        }
                        if (nhs.Count > maxSetSize)
                        {
                            maxSetSize = nhs.Count;
                        }
                    }

                    HashSet<HashSet<int>> allSets = new HashSet<HashSet<int>>();
                    int totalCount = 0;
                    foreach (var setItem in sets)
                    {
                        var fs = setItem.Value;
                        if (allSets.Add(fs))
                        {
                            totalCount += fs.Count;
                        }
                    }
                    Console.WriteLine("found {0} duplicates in {1} sets with {2} bits diff, max set: {3}",
                        totalCount - allSets.Count, allSets.Count, binDiff,maxSetSize);
                    //if (maxSetSize > MaximalSetSize)
                    //    break;
                    var setFileName = string.Format(SetFile, binDiff);
                    using (var setWriter = File.CreateText(setFileName))
                    {
                        foreach (var set in allSets)
                        {
                            List<int> pics = new List<int>();
                            pics.AddRange(set);
                            setWriter.WriteLine("{0}", fileNames[pics[0]]);
                            for (int i = 1; i < pics.Count; ++i)
                                setWriter.WriteLine("\t{0}", fileNames[pics[i]]);
                        }
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


        private void match(List<int> picList)
        {
            var w4it = new Wait4It();
            try
            {
                if (picList.Count <= 1)
                    return;
                var pic2Bytes = new byte[ChunkSize];
                var pic1Bytes = new byte[ChunkSize];
                for (int picNum2 = 1; picNum2 < picList.Count; ++picNum2)
                {
                    var pic2 = picList[picNum2];
                    lock (mmfLock)
                    {
                        acc.ReadArray(pic2 * ChunkSize, pic2Bytes, 0, ChunkSize);
                    }
                    for (int picNum1 = 0; picNum1 < picNum2; ++picNum1)
                    {
                        var pic1 = picList[picNum1];
                        lock (considered)
                        {
                            var t = Tuple.Create(pic1, pic2);
                            var b = considered.Add(t);
                            if (!b) // have it already
                                continue;
                        }
                        lock (mmfLock)
                        {
                            acc.ReadArray(pic1 * ChunkSize, pic1Bytes, 0, ChunkSize);
                        }

                        int maxDiff = 0;
                        int totalDiff = 0;
                        for (var k = 0; k < ChunkSize; ++k)
                        {
                            int d = pic1Bytes[k] - pic2Bytes[k];
                            d = Math.Abs(d);
                            // looking for the nearest match
                            for (int s = 0; s < shiftPos.Length; ++s)
                            {
                                int p = k + shiftPos[s];
                                if (p < 0 || p >= ChunkSize)
                                    continue;
                                int d2 = pic1Bytes[p] - pic2Bytes[k];
                                d2 = Math.Abs(d2);
                                if (d2 < d)
                                    d = d2;
                            }
                            if (maxDiff < d)
                                maxDiff = d;
                            totalDiff += d;
                        }
                        ++diffCounts[maxDiff];
                        // Console.Out.WriteLine("got maxdiff {0}", maxDiff);
                        if (maxDiff >= MaxDiff)
                            continue;
                        lock (matches)
                        {
                            var t = Tuple.Create(pic1, pic2);
                            matches[maxDiff].Add(t);
                        }
                    }
                }
            }
            finally
            {
                lock (countLock)
                {
                    matchesDone += picList.Count * (picList.Count - 1) / 2;
                }
            }

        }



    }
}