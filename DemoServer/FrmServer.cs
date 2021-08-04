using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LeslieXin.SimpleMMF;

namespace DemoServer
{
    public partial class FrmServer : Form
    {
        public FrmServer()
        {
            InitializeComponent();
        }

        SimpleMMF simpleMMF;

        void TboxAppend(string s)
        {
            if (textBox2.InvokeRequired)
                textBox2.Invoke(new Action<string>(TboxAppend), new object[] { s });
            else
            {
                textBox2.AppendText(s+"\r\n");
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            simpleMMF = new SimpleMMF("Server001");
            simpleMMF.ServerMsg += SimpleMMF_ServerMsg;
        }

        private void SimpleMMF_ServerMsg(object sender, KeyValuePair<string, string> e)
        {
            TboxAppend($"收到|客户端名称：{e.Key}，信息：{e.Value}");
            TboxAppend($"写入|【{e.Value}】");
            TboxAppend($"－－－－－－");
            simpleMMF.MMFWrite($"【{e.Value}】");
        }

    }
}
