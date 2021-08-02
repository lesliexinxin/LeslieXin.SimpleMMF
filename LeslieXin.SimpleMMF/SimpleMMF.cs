using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;

/* Author: Leslie Xin
 * E-Mail: lesliexin@outlook.com
 * WebSite: http://www.lesliexin.com
 * Datetime: 2021-08-03 06:55:03
 */ 

namespace LeslieXin.SimpleMMF
{
    public class SimpleMMF
    {
        public SimpleMMF(string serverName)
        {
            Role = MMFRole.Server;
            ServerName = serverName;
            ClientName = "";
            InitWorker();
        }

        public SimpleMMF(string serverName, string clientName)
        {
            Role = MMFRole.Server;
            ServerName = serverName;
            ClientName = clientName;
            InitWorker();
        }

        ~SimpleMMF()
        {
            worker.CancelAsync();
            MMFDispose();
        }

        private string ServerName;
        private string ClientName;
        private bool IsBusy;
        private MMFRole Role;
        private enum MMFRole { Server,Client}
        private enum MMFType { STATE,VALUE,CLIENT}
        private BackgroundWorker worker;
        private static readonly object locker = new object();

        public event EventHandler<string> ServerMsg;
        public event EventHandler<KeyValuePair<string, string>> ClientMsg;

        private void InitWorker()
        {
            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!e.Cancel)
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    continue;
                }
                if (IsBusy) continue;

                string state = MMFRead(MMFType.STATE);
                if (string.IsNullOrEmpty(state)) continue;
                if (state == "1")
                {
                    MMFWrite(MMFType.STATE, "0");
                    string msg = MMFRead(MMFType.VALUE);
                    string client = MMFRead(MMFType.CLIENT);
                    ClientMsg?.Invoke(this, new KeyValuePair<string, string>(client, msg));
                    continue;
                }
                if (state == "2")
                {
                    MMFWrite(MMFType.STATE, "0");
                    string msg = MMFRead(MMFType.VALUE);
                    ServerMsg?.Invoke(this,msg);
                    continue;
                }
            }
        }

        public void MMFWrite(string msg)
        {
            IsBusy = true;
            MMFWrite(MMFType.VALUE, msg);
            MMFWrite(MMFType.STATE, Role == MMFRole.Server ? "1" : "2");
            if (Role == MMFRole.Client)
                MMFWrite(MMFType.CLIENT, ClientName);
            IsBusy = false;
        }

        private void MMFWrite(MMFType type, string msg)
        {
            long capacity = 1 << 10 << 10 << 10;
            var mmf = MemoryMappedFile.CreateOrOpen($"{ServerName}{type.ToString()}", capacity, MemoryMappedFileAccess.ReadWrite);
            lock (locker)
            {
                using (var accessor = mmf.CreateViewAccessor(0, capacity))
                {
                    accessor.Write(0, msg.Length);
                    accessor.WriteArray<char>(sizeof(Int32), msg.ToArray(), 0, msg.Length);
                }
            }
        }

        private string MMFRead(MMFType type)
        {
            long capacity = 1 << 10 << 10 << 10;
            var mmf = MemoryMappedFile.CreateOrOpen($"{ServerName}{type.ToString()}", capacity, MemoryMappedFileAccess.ReadWrite);
            lock (locker)
            {
                using (var accessor = mmf.CreateViewAccessor(0, capacity))
                {
                    int strLen = accessor.ReadInt32(0);
                    char[] chars = new char[strLen];
                    accessor.ReadArray<char>(sizeof(Int32), chars, 0, strLen);
                    return new string(chars);
                }
            }
        }

        private void MMFDispose()
        {
            try
            {
                var mmf = MemoryMappedFile.OpenExisting($"{ServerName}STATE");
                mmf.Dispose();
            }
            catch (Exception)
            {
            }
            try
            {
                var mmf = MemoryMappedFile.OpenExisting($"{ServerName}VALUE");
                mmf.Dispose();
            }
            catch (Exception)
            {
            }
            try
            {
                var mmf = MemoryMappedFile.OpenExisting($"{ServerName}CLIENT");
                mmf.Dispose();
            }
            catch (Exception)
            {
            }
        }

    }
}
