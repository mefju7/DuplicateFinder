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
        ConcurrentBag<Task> allTasks=new ConcurrentBag<Task>();
        object myLock = new object(); // just for synchronizing
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
                foreach (var a in args)
                {
                    var t = Task.Run(() => scan(a));
                    allTasks.Add(t);
                }
                Task tw4;
                while (allTasks.TryTake(out tw4))
                {
                    // Console.Out.WriteLine("waiting for task");
                    tw4.Wait();
                }
               
            }
        }

        private void scan(string dir)
        {
            Console.Out.WriteLine("Scanning for {0}", dir);
           
            if (Directory.Exists(dir))
            {
               var dirs=Directory.GetDirectories(dir);
                foreach(var d in dirs)
                {
                    var t = Task.Run(() => scan(d));
                    allTasks.Add(t);
                }
                var files = Directory.EnumerateFiles(dir);
                foreach(var f in files)
                {
                    var t = Task.Run(() => getThumb(f));
                    allTasks.Add(t);
                    if (allTasks.Count > 1000) {
                        Console.Out.WriteLine("sleeping");
                        Thread.Sleep(5000);
                    }
                }
            }
        }

        private void getThumb(string f)
        {
            Console.Out.WriteLine("creating thumb for {0}", f);
            try
            {
                using (var img = Image.FromFile(f))
                {
                    int h = img.Height;
                    int w = img.Width;
                    if (h > 16) h = 16;
                    if (w > 16) w = 16;
                    using (var bmp = new Bitmap(img, w, h))
                    {
                        var b=new byte[ChunkSize];
                        for(int i = 0; i < bmp.Height; ++i)
                        {
                            int row = 3 * 16 * i;
                            for(int j = 0; j < bmp.Width; ++j)
                            {
                                int idx = row + j * 3;
                                var c=bmp.GetPixel(j, i);
                                b[idx] = c.R;
                                b[idx + 1] = c.G;
                                b[idx + 2] = c.B;
                            }
                        }
                        store(f, b);
                    }
                }
            }
            catch(OutOfMemoryException )
            {
                // ignore
            }
            finally { }
        }

        private void store(string f, byte[] b)
        {
            lock(myLock)
            {
                txtFile.WriteLine(f);
                binFile.Write(b, 0, ChunkSize);
            }
        }
    }
}