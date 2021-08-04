using LeslieXin.SimpleMMF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DemoClient
{
    public partial class FrmClient : Form
    {
        public FrmClient()
        {
            InitializeComponent();
        }

        SimpleMMF simpleMMF;

        void TboxText(string s)
        {
            if (textBox2.InvokeRequired)
                textBox2.Invoke(new Action<string>(TboxText), new object[] { s });
            else
            {
                textBox2.Text = s;
            }
        }

        private void FrmClient_Load(object sender, EventArgs e)
        {
            simpleMMF = new SimpleMMF("Server001", "Client001");
            simpleMMF.ClientMsg += SimpleMMF_ClientMsg;
        }

        private void SimpleMMF_ClientMsg(object sender, KeyValuePair<string, string> e)
        {
            if(e.Key== textBox3.Text)
            {
                TboxText(e.Value);
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            simpleMMF.MMFWrite(textBox1.Text);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            simpleMMF = new SimpleMMF("Server001", "Client002");
        }
    }
}
