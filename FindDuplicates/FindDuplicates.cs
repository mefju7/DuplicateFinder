﻿using System;
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
        
        object myLock = new object(); // just for synchronizing
        Dictionary<Tuple<long, long>, Boolean> matches = new Dictionary<Tuple<long, long>, bool>();
        private MemoryMappedViewAccessor acc;
        private Random rnd;
        private long totalMatches = 0;

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
                    Console.Out.WriteLine("Let's start...");
                    var n1 = DateTime.Now;
                    Thread.Sleep(2000);
                    while (Wait4It.Working)
                    {
                        var wUntil = DateTime.Now;
                        Console.WriteLine(" {0} -- {1}", totalMatches, wUntil.ToString("HH:mm:ss"));
                        Thread.Sleep(2000);
                    }
                }
                Console.WriteLine("total amount of matches {0}", totalMatches);
                var trueMatches = new Dictionary<Tuple<long, long>, bool>();
                foreach (var k in matches)
                {
                    if (k.Value)
                        trueMatches.Add(k.Key, k.Value);
                }
                Console.WriteLine("matched pairs = {0}", trueMatches.Count);
                var allIdx = new long[cnt];
                for (int i = 0; i < cnt; ++i)
                    allIdx[i] = i;
                bool again;
                do {
                    again = false;
                    foreach(var k in trueMatches)
                    {
                        var opic = k.Key.Item1;
                        var pic = k.Key.Item2;
                        if(opic>pic)
                        {
                            Console.Error.WriteLine("Hm...");
                        }
                        if(allIdx[pic]>allIdx[opic])
                        {
                            again = true;
                            allIdx[pic] = allIdx[opic];
                        }
                    }
                } while (again);
                var allSets = new Dictionary<long, List<long> > ();
                for(int i = 0; i < cnt; ++i)
                {
                    var ai = allIdx[i];
                    if (ai != i)
                    {
                        List<long> ll;
                       if(! allSets.TryGetValue(ai, out ll))
                        {
                            ll = new List<long>();
                            ll.Add(ai);
                            allSets.Add(ai, ll);
                        }
                        ll.Add(i);
                    }
                }
                using (var setWriter = File.CreateText("sets.txt"))
                {
                    foreach(var sl in allSets)
                    {
                        var ll=sl.Value;
                        String line = String.Format("+{0}", ll[0]+1);
                        setWriter.WriteLine(line);
                        for (int i = 1; i < ll.Count; ++i)
                        {
                            line = String.Format("-{0}", ll[i] + 1);
                            setWriter.WriteLine(line);
                        }
                    }
                }
            }
        }

        private void analyze(List<long> picList)
        {
            var w4it = new Wait4It();
            // the discriminator
            var disc = new double[ChunkSize];
            for (int i = 0; i < ChunkSize; ++i)
                disc[i] = rnd.NextDouble() - 0.5;
            var leftPic = new double[ChunkSize];
            long leftPictures = 1;
            long rightPictures = 1;
            double middleValue = 0;
            double lowest = 0, highest = 0;
            var rightPic = new double[ChunkSize];
            var picBytes = new byte[ChunkSize];
            for (int iter = 0; iter < 100; ++iter)
            {
                /*
                leftPictures = rightPictures = 1;
                for (int i = 0; i < ChunkSize; ++i)
                    leftPic[i] = rightPic[i] = 0;
                */
                lowest = Double.PositiveInfinity;
                highest = Double.NegativeInfinity;
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
            var lists = new List<long>[3];
            for (int i = 0; i < 3; ++i)
                lists[i] = new List<long>();
            var middleLeft = (3 * lowest + 2 * highest) / 5;
            var middleRight = (2 * lowest + 3 * highest) / 5;
            foreach (var pic in picList)
            {
                acc.ReadArray(pic * ChunkSize, picBytes, 0, ChunkSize);
                double total = 0;
                for (int i = 0; i < ChunkSize; ++i)
                    total = disc[i] * picBytes[i];
                if (total > middleValue)
                {
                    lists[2].Add(pic);
                    if (total < middleRight)
                        lists[1].Add(pic);
                }
                else
                {
                    lists[0].Add(pic);
                    if (total > middleLeft)
                        lists[1].Add(pic);
                }
            }
            Console.Out.WriteLine("Lists {0} {1} {2}", lists[0].Count, lists[1].Count, lists[2].Count);
            for (int i = 0; i < 3; ++i)
            {
                var l = lists[i];
                if (l.Count > 0)
                {
                    Task t;
                    if (l.Count < 16)
                        t = Task.Run(() => match(l));
                    else
                        t = Task.Run(() => analyze(l));
                   
                }
            }
        }

        private void match(List<long> l)
        {
            var w4it = new Wait4It();
            Console.Out.WriteLine("matching list with {0}", l.Count);
            var picBytes = new byte[ChunkSize];
            var otherPicBytes = new byte[ChunkSize];
            for (int i = 1; i < l.Count; ++i)
            {
                var pic = l[i];
                acc.ReadArray(pic * ChunkSize, picBytes, 0, ChunkSize);
                for (int j = 0; j < i; ++j) // only halv
                {
                    var opic = l[j];
                    if (haveIt(opic, pic))
                    {
                        // Console.Out.WriteLine("not doing match twice");
                        continue;
                    }
                    ++totalMatches;
                    acc.ReadArray(opic * ChunkSize, otherPicBytes, 0, ChunkSize);
                    double total = 0;
                    for (int k = 0; k < ChunkSize; ++k)
                        total += Math.Abs(otherPicBytes[k] - picBytes[k]);
                    total /= ChunkSize;
                    if (total < 255/5) // 5% difference 
                    {
                        Console.Out.WriteLine("seems similar {0} {1}", opic, pic);
                        submit(opic, pic, true);
                    }
                    else submit(opic, pic, false);
                }
            }
        }

        private bool haveIt(long opic, long pic)
        {
            lock (myLock)
            {
                return matches.ContainsKey(new Tuple<long, long>(opic, pic));
            }
        }

        private void submit(long opic, long pic, bool v)
        {
            lock (myLock)
            {

                var key = new Tuple<long, long>(opic, pic);
                bool b;
                if (matches.TryGetValue(key, out b))
                {
                    if (b != v)
                        Console.Out.WriteLine("the horror");
                }
                else
                    matches.Add(key, v);
            }
        }
    }
}