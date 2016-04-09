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
            progress.Visible = false;
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
                progress.Value = 0;
                progress.Visible = true;
                progress.Maximum = lines.Length;
                fileSets.Clear();
                var lastSet =new  List<String>();
                int i = 0;
                int setMax = 0;
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
                    if (lastSet.Count > setMax)
                        setMax = lastSet.Count;
                }
                trackBar1.Minimum = 0;
                trackBar1.Maximum = fileSets.Count-1;
                Text = string.Format("{0} sets of total {1} files", fileSets.Count, lines.Length);
                for(int c = 1; c < setMax; ++c)
                {
                    if(c*c > setMax)
                    {
                        tabPanel.ColumnCount = c;
                        tabPanel.RowCount = c;
                        break;
                    }
                }
                progress.Visible = false;
                trackBar1.Value = trackBar1.Maximum;
            }
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            int num = trackBar1.Maximum - trackBar1.Value;
            tabPanel.Controls.Clear();
     
            var picList = fileSets[num];
            int cols = 1;
            var count = picList.Count;
            if (count > 64)
                count = 64;
            for (; cols * cols < count; ++cols) ;
            for (int i = 0; i < count; ++i)
            {
                var pb = new PictureBox();
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                //pb.LoadCompleted += Pb_LoadCompleted;
                pb.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                tabPanel.Controls.Add(pb,i% cols,i/cols);
                pb.LoadAsync(picList[i]);
            }
            statusLine.Text = string.Format("loaded set {0}", num + 1);
        }
        
    }
}
