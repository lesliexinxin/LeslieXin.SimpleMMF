using System;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;

namespace LeslieXin.SimpleMMF
{
    /* Author: Leslie Xin
     * E-Mail: lesliexin@outlook.com
     * WebSite: http://www.lesliexin.com
     * Datetime: 2021-08-03 06:55:03
     * 
     * V2
     */



    /// <summary>
    /// LeslieXin.SimpleMMF
    /// </summary>
    public class SimpleMMF
    {
        /// <summary>
        /// 实例化服务
        /// </summary>
        /// <param name="serverName">服务名称</param>
        /// <param name="role">监测规则</param>
        /// <param name="resetRole">事件重置方式，默认：WaitFor</param>
        public SimpleMMF(string serverName, MMFRole role, MMFResetRole resetRole = MMFResetRole.WaitFor)
        {
            Role = role;
            ResetRole = resetRole;
            ServerName = serverName;
            InitWorker();
        }


        ~SimpleMMF()
        {
            worker.CancelAsync();
            MMFDispose();
        }

        private string ServerName;
        private bool IsBusy;
        private MMFRole Role;
        private MMFResetRole ResetRole;
        /// <summary>
        /// 监测规则
        /// </summary>
        public enum MMFRole
        {
            /// <summary>
            /// Server方
            /// </summary>
            Server,
            /// <summary>
            /// Client方
            /// </summary>
            Client
        }
        /// <summary>
        /// 事件重置方式
        /// </summary>
        public enum MMFResetRole
        {
            /// <summary>
            /// 事件调用执行后再重置
            /// </summary>
            WaitFor,
            /// <summary>
            /// 事件调用执行前重置
            /// </summary>
            Return
        }

        private BackgroundWorker worker;
        private static readonly object locker = new object();

        private EventWaitHandle _eventClient, _eventServer;

        /// <summary>
        /// 监测规则为Server时，实现此事件，以获取写入的消息
        /// </summary>
        public event EventHandler<string> ServerMsgReceived;
        /// <summary>
        /// 监测规则为Client时，实现此事件，以获取写入的消息
        /// </summary>
        public event EventHandler<string> ClientMsgReceived;

        private void InitWorker()
        {
            _eventClient = new EventWaitHandle(false, EventResetMode.ManualReset, ServerName + "_Client");
            _eventServer = new EventWaitHandle(false, EventResetMode.ManualReset, ServerName + "_Server");

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

                if (Role == MMFRole.Client)
                {
                    _eventServer.WaitOne();
                    string msg = MMFRead();
                    if (ResetRole == MMFResetRole.Return)
                    {
                        _eventServer.Reset();
                    }
                    ClientMsgReceived?.Invoke(this, msg);
                    if (ResetRole == MMFResetRole.WaitFor)
                    {
                        _eventServer.Reset();
                    }
                }

                else if (Role == MMFRole.Server)
                {
                    _eventClient.WaitOne();
                    string msg = MMFRead();
                    if (ResetRole == MMFResetRole.Return)
                    {
                        _eventClient.Reset();
                    }
                    ServerMsgReceived?.Invoke(this, msg);
                    if (ResetRole == MMFResetRole.WaitFor)
                    {
                        _eventClient.Reset();
                    }
                }

            }
        }

        /// <summary>
        /// 向共享内存中写入信息，写入后会自动发送给对方
        /// </summary>
        /// <param name="msg">待写入信息</param>
        public void MMFWrite(string msg)
        {
            IsBusy = true;
            long capacity = 1 << 10 << 10 << 10;
            var mmf = MemoryMappedFile.CreateOrOpen($"{ServerName}", capacity, MemoryMappedFileAccess.ReadWrite);
            lock (locker)
            {
                using (var accessor = mmf.CreateViewAccessor(0, capacity))
                {
                    accessor.Write(0, msg.Length);
                    accessor.WriteArray<char>(sizeof(Int32), msg.ToArray(), 0, msg.Length);
                }
            }
            if (Role == MMFRole.Client)
            {
                _eventClient.Set();
            }
            else if (Role == MMFRole.Server)
            {
                _eventServer.Set();
            }
            IsBusy = false;
        }

        private string MMFRead()
        {
            long capacity = 1 << 10 << 10 << 10;
            var mmf = MemoryMappedFile.CreateOrOpen($"{ServerName}", capacity, MemoryMappedFileAccess.ReadWrite);
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
                var mmf = MemoryMappedFile.OpenExisting($"{ServerName}");
                mmf.Dispose();
            }
            catch (Exception)
            {
            }
            try
            {
                _eventClient?.Dispose();
            }
            catch (Exception)
            {
            }
            try
            {
                _eventServer?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// string -> byte[] ，UTF8
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public byte[] Convert(string msg,Encoding encoding)
        {
            return Encoding.UTF8.GetBytes(msg);
        }

        /// <summary>
        /// string -> byte[]，UTF8
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public string Convert(byte[] msg)
        {
            return Encoding.UTF8.GetString(msg);
        }
    }
}
