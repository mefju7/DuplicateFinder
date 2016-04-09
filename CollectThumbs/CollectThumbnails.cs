using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ThumbCollector
{
    internal class CollectThumbnails
    {
        private const int ChunkSize = 3 * 16 * 16;

        private const int DisplayDelay = 2;
        private const double blurStDev = 4;
        object myLock = new object(); // just for synchronizing
        long saved = 0;
        object countLock = new object();
        long f2scan = 0;
        long finished = 0;
        private StreamWriter txtFile;
        private FileStream binFile;
        private StreamWriter blackFile;
        private Image.GetThumbnailImageAbort myThumbnailCallback;
        private int[] shiftPos;
        private double[] blurring;

        public CollectThumbnails()
        {
            myThumbnailCallback = new Image.GetThumbnailImageAbort(ThumbnailCallback);
            shiftPos = new int[25];
            blurring = new double[25];
            int k=0;
            for (int i = -2; i <= 2; ++i)
                for(int j=-2;j<= 2; ++j,++k)
                {
                    shiftPos[k] = (i * 16+j)*3; // remember 3 bytes per pixel
                    double d = (i * i + j * j);
                    blurring[k] = Math.Exp(-d / blurStDev);
                }
        }

        public bool ThumbnailCallback()
        {
            Console.Out.WriteLine("thumbnail callback");
            return false;
        }

        internal void run(string[] args)
        {
            Console.Out.WriteLine("storing data in {0}",Directory.GetCurrentDirectory());
            using (txtFile = File.CreateText("files.txt"))
            using (blackFile = File.CreateText("blacks.txt"))
            using (binFile = File.Create("files.bin"))
            {
                {
                    foreach (var a in args)
                    {
                        Console.Out.WriteLine("start scanning from {0}", a);
                        var t = Task.Run(() => scan(a));
                    }
                }
                Console.Out.WriteLine("Let's start...");
                Thread.Sleep(500);
                var startedAt = DateTime.Now;
                while (Wait4It.Working)
                {
                    double t1 = finished;
                    double t2 = f2scan;
                    double per = 0;
                    var finishAt = DateTime.Now;
                    if (t2 > 0)
                    {
                        per = t1 / t2;
                        var ts = finishAt.Subtract(startedAt);
                        double s2w = 0;
                        if (t1 > 0)
                            s2w = ts.TotalSeconds * (t2 - t1) / t1;
                        finishAt = finishAt.AddSeconds(s2w);
                    }
                    Console.WriteLine("scanning: {0:#,0} / {1:#,0} ({2:0%}) => {3} -- {4}", t1, t2, per, saved, finishAt.ToString("HH:mm:ss"));
                    Thread.Sleep(5000);
                }
            }
        }

        private void scan(string dir)
        {
            var w4i = new Wait4It();
            try
            {
                if (Directory.Exists(dir))
                {
                    var dirs = Directory.GetDirectories(dir);
                    foreach (var d in dirs)
                    {
                        var t = Task.Run(() => scan(d));
                    }
                    var files = Directory.EnumerateFiles(dir);
                    foreach (var f in files)
                    {
                        lock (countLock)
                        {
                            ++f2scan;
                        }
                        var t = Task.Run(() => getThumb(f));
                    }
                }
            }
            catch (Exception) { }
        }

        private void getThumb(string f)
        {
            var w4i = new Wait4It();

            try
            {
                using (var img = Image.FromFile(f))
                {
                    // Console.Out.WriteLine("creating thumb for {0}", f);
                    int h = img.Height;
                    int w = img.Width;
                    if (h > 16) h = 16;
                    if (w > 16) w = 16;
                    Image tmb;
                    using (tmb=img.GetThumbnailImage(w,h,myThumbnailCallback,IntPtr.Zero))
                    using (var bmp = new Bitmap(tmb, w, h))
                    {
                        var b = new byte[ChunkSize];
                        for (int i = 0; i < bmp.Height; ++i)
                        {
                            int row = 3 * 16 * i;
                            for (int j = 0; j < bmp.Width; ++j)
                            {
                                int idx = row + j * 3;
                                var c = bmp.GetPixel(j, i);
                                b[idx] = c.R;
                                b[idx + 1] = c.G;
                                b[idx + 2] = c.B;
                            }
                        }
                        var b2 = new byte[ChunkSize];
                        for(int i = 0; i < ChunkSize; ++i)
                        {
                            double d = 0;
                            double w2 = 0;
                            for(int j = 0; j < shiftPos.Length; ++j)
                            {
                                int k = i + shiftPos[j];
                                if ((k < 0) || (k >= ChunkSize))
                                    continue;
                                w2 += blurring[j];
                                d += blurring[j] * b[k];
                            }
                            b2[i] = (byte) Math.Floor(d / w2);
                        }
                        int total = 0;
                        for (int i = 0; i < ChunkSize; ++i)
                            total += b2[i];
                        if (total > 0)
                            store(f, b);
                        else
                            storeBlack(f);
                    }
                }
            }
            catch (Exception)
            {
                // ignore all exceptions
            }
            finally
            {
                lock (countLock)
                {
                    ++finished;
                }
            }
        }

        private void storeBlack(string f)
        {
            lock (myLock)
            {
                blackFile.WriteLine(f);
                ++saved;
            }
        }

        private void store(string f, byte[] b)
        {
            lock (myLock)
            {

                txtFile.WriteLine(f);
                binFile.Write(b, 0, ChunkSize);
                ++saved;
            }
        }


    }
}