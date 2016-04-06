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
        Semaphore sem = new Semaphore(1, 1);
        // ConcurrentBag<Task> allTasks = new ConcurrentBag<Task>();
        object myLock = new object(); // just for synchronizing
        long saved = 0;
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
                var wait4it = new Wait4It(sem);
                foreach (var a in args)
                {
                    var t = Task.Run(() => scan(a));
                    // allTasks.Add(t);
                }
                Task t2w4;
                var last = DateTime.Now.AddSeconds(DisplayDelay);
                long finished = 0;
                while (allTasks.TryTake(out t2w4))
                {
                    ++finished;
                    if (t2w4.IsCompleted)
                        continue;
                    t2w4.Wait();
                    var n=DateTime.Now;
                    if(n > last)
                    {
                        var t = allTasks.Count;
                        Console.WriteLine("{0} / {1} => {2}", finished, finished+t,saved);
                        last = n.AddSeconds(DisplayDelay);
                    }
                }

            }
        }

        private void scan(string dir)
        {
            // Console.Out.WriteLine("Scanning for {0}", dir);
            try
            {
                if (Directory.Exists(dir))
                {
                    var dirs = Directory.GetDirectories(dir);
                    foreach (var d in dirs)
                    {
                        var t = Task.Run(() => scan(d));
                        allTasks.Add(t);
                    }
                    var files = Directory.EnumerateFiles(dir);
                    foreach (var f in files)
                    {
                        var t = Task.Run(() => getThumb(f));
                        allTasks.Add(t);
                    }
                }
            }
            catch (Exception) { }
        }

        private void getThumb(string f)
        {
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
            catch (OutOfMemoryException)
            {
                // ignore
            }
            finally { }
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