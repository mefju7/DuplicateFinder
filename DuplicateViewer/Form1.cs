using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DuplicateViewer
{
    public partial class Form1 : Form
    {
        List<List<String>> fileSets = new List<List<String>>();
        public Form1()
        {
            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void openSetFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            var r=ofd.ShowDialog();
            if(r== DialogResult.OK)
            {
                var lines = File.ReadAllLines(ofd.FileName);
                progress.Maximum = lines.Length;
                fileSets.Clear();
                var lastSet =new  List<String>();
                int i = 0;
                foreach (var line in lines)
                {
                    progress.Value = ++i;
                    if (line.StartsWith("\t"))
                    {
                        lastSet.Add(line.Substring(1));
                    }
                    else
                    {
                        var nl = new List<String>();
                        nl.Add(line);
                        lastSet = nl;
                        fileSets.Add(nl);
                    }
                }
                trackBar1.Minimum = 0;
                trackBar1.Maximum = fileSets.Count;
                statusLine.Text = string.Format("{0} sets of total {1} files", fileSets.Count, lines.Length);
            }
        }
    }
}
