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
        object myLock = new object(); // just for synchronizing
        long saved = 0;
        object countLock = new object();
        long f2scan = 0;
        long finished = 0;
        private StreamWriter txtFile;
        private FileStream binFile;

        public CollectThumbnails()
        {
        }

        internal void run(string[] args)
        {
            using (txtFile = File.CreateText("files.txt"))
            using (binFile = File.OpenWrite("files.bin"))
            {
                {
                    foreach (var a in args)
                    {
                        var t = Task.Run(() => scan(a));
                    }
                }
                Console.Out.WriteLine("Let's start...");
                Thread.Sleep(2000);
                var n = DateTime.Now;
                while(Wait4It.Working)
                {
                    double t1 = finished;
                    double t2 = f2scan;
                    double per = 0;
                    var finishAt = DateTime.Now;
                    if (t2 > 0)
                    {
                        per = t1 / t2;
                        var n2 = DateTime.Now;
                        var ts = n2.Subtract(n);
                        var s2w=ts.TotalSeconds * (t2 - t1) / t2;
                        finishAt= n2.AddSeconds(s2w);
                    }
                    Console.WriteLine("scanning: {0:0,0} / {1:0,0} ({2:0%}) => {3} -- {4}",t1,t2,per, saved,finishAt.ToString("HH:mm:ss"));
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
                    using (var bmp = new Bitmap(img, w, h))
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
                        store(f, b);
                    }
                }
            }
            catch (Exception)
            {
                // ignore all exceptions
            }
            finally {
                lock (countLock)
                {
                    ++finished;
                }
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