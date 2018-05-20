using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmguC
{
    public partial class Form2 : Form

    {
        public Form2()
        {
            InitializeComponent();
        }
        public string fileName = "Settings.inf";     //пишем  путь к файлу с настройками
        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();

            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            string fileName = "Settings.inf";                 //пишем  путь к файлу           
                using (StreamWriter sw = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.Write))) {
                    sw.WriteLine(trackBar1.Value);             //пишем
                    sw.WriteLine(trackBar2.Value);
                    sw.WriteLine(trackBar3.Value);
                    sw.WriteLine(trackBar4.Value);
                    sw.WriteLine(trackBar5.Value);
                    sw.WriteLine(trackBar6.Value);
                    sw.WriteLine(trackBar7.Value);
                    sw.WriteLine(Data.posRoi1);
                    sw.WriteLine(Data.posRoi2);
                }               

            Hide();
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
