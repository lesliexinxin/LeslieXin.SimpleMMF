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
    /// <summary>
    /// LeslieXin.SimpleMMF
    /// </summary>
    public class SimpleMMF
    {
        /// <summary>
        /// 作为服务端实例化（instantize as a server）
        /// </summary>
        /// <param name="serverName">服务端名称（server name）<para>需要与客户端的服务端名称保持一致（need to be consistent with the client's server name）</para></param>
        public SimpleMMF(string serverName)
        {
            Role = MMFRole.Server;
            ServerName = serverName;
            ClientName = "";
            InitWorker();
        }

        /// <summary>
        /// 作为客户端实例化（instantize as a client）
        /// </summary>
        /// <param name="serverName">服务端名称（server name）<para>需要与服务端的服务端名称保持一致（need to be consistent with the server name on the service side）</para></param>
        /// <param name="clientName">客户端名称（client name）</param>
        public SimpleMMF(string serverName, string clientName)
        {
            Role = MMFRole.Client;
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

        /// <summary>
        /// 作为服务端启动时响应此事件（respond to this event when started as a server）<para>参数e为客户端写入的信息（argument e is the information written by the client）</para><para>key：客户端名称（key: client name）</para><para>value：客户端写入信息（key: the information written by the client）</para>
        /// </summary>
        public event EventHandler<KeyValuePair<string, string>> ServerMsg;
        /// <summary>
        /// 作为客户端启动时响应此事件（respond to this event when started as a client）<para>参数e为服务端写入的信息（argument e is the information written by the server）</para><para>key：客户端名称（key: client name）</para><para>value：服务端写入信息（key: the information written by the server）</para>
        /// </summary>
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
                    //内容发生一次改变则只会触发一次事件，之后状态就复原，不能一直去触发。
                    MMFWrite(MMFType.STATE, "0");
                    if (Role == MMFRole.Client)
                    {
                        string msg = MMFRead(MMFType.VALUE);
                        string client = MMFRead(MMFType.CLIENT);
                        ClientMsg?.Invoke(this, new KeyValuePair<string, string>(client, msg));
                    }
                    continue;
                }
                if (state == "2")
                {
                    //内容发生一次改变则只会触发一次事件，之后状态就复原，不能一直去触发。
                    MMFWrite(MMFType.STATE, "0");
                    if (Role == MMFRole.Server)
                    {
                        string msg = MMFRead(MMFType.VALUE);
                        string client = MMFRead(MMFType.CLIENT);
                        ServerMsg?.Invoke(this, new KeyValuePair<string, string>(client, msg));
                    }
                    continue;
                }
            }
        }

        /// <summary>
        /// 向共享内存中写入信息（write information to shared memory）
        /// </summary>
        /// <param name="msg">待写入信息（the information to be written）</param>
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
