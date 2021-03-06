﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tanki
{
    public abstract class MessageQueueAbs : IMessageQueue
    {
        private MessageQueueAbs() { }
        public MessageQueueAbs(IEngine withEngine = null) { if (withEngine != null) _serverEngine = withEngine; }

        public IMessageQueueClient Owner { get; protected set; }
        protected IEngine _serverEngine;

        public abstract void RUN();
        public abstract void STOP();
        public abstract void Enqueue(IPackage msg);
        public abstract void Dispose();

        public void OnRegistered_EventHandler(object Sender, RegMsgQueueData evntData)
        {
            Owner = evntData.MsgQueueOwner;

            if (Owner.Engine == null) throw new Exception("Engine объекта IMessageQueueClient - еще не зарегистрирован..");
            _serverEngine = Owner.Engine;
        }
    }



    public class MessageQueue_ProcessedOneByOne : MessageQueueAbs
    {
        public MessageQueue_ProcessedOneByOne(IEngine withEngine, Object data = null) : base(withEngine) { }

        private Object _locker = new Object();
        private Object _locker_stopping = new Object();
        private AutoResetEvent _ifReady = new AutoResetEvent(false);
        private AutoResetEvent _proceedMsg = new AutoResetEvent(false);
        private AutoResetEvent _timer = new AutoResetEvent(false);

        //private MAK_MSG_proceed_method<T> _msg_proceed_method;

        private Queue<IPackage> _msg_queue = new Queue<IPackage>();
        private Thread _proceedingThread = null;
        private Boolean _enforceCancel = false;
        private CancellationTokenSource _cancelQueueTokenSource = new CancellationTokenSource();
        private CancellationToken _cancelQueueToken;

        //public MAK_MSG_proceed_method<T> MsgProceedMethod { get { return _msg_proceed_method; } }
        public Thread ProceedingThread { get { return _proceedingThread; } }
        public Boolean EnforceCancel { get { return _enforceCancel; } set { _enforceCancel = value; } }

        public override void Enqueue(IPackage newMsg)
        {
            lock (_locker)
            {
                _msg_queue.Enqueue(newMsg);

                var s = _proceedingThread.ThreadState;
                _ifReady.Set();
            }
        }

        public override void RUN()
        {
            _cancelQueueToken = _cancelQueueTokenSource.Token;
            _proceedingThread = new Thread(ProceedQueue);
            _proceedingThread.Name = "MSG_PROCEEDING_ONEBYONE";
            _proceedingThread.Start();
        }

        private void ProceedQueue()
        {
            try
            {
                while (true)
                {
                    IPackage msg = null;

                    lock (_locker)
                    {
                        if (_cancelQueueToken.IsCancellationRequested)
                        {
                            _cancelQueueToken.ThrowIfCancellationRequested();
                        }

                        if (_msg_queue.Count > 0)
                            msg = _msg_queue.Dequeue();
                    }

                    if (msg != null)
                    {
                        _serverEngine.ProcessMessage(msg); //- НУЖНА ЕЩЕ РЕАЛИЗАЦИЯ ProcessMessage  c параметром 'просто единичный IProtocol'
                    }
                    else
                        _ifReady.WaitOne();

                }

                //_proceedMsg.Set();

            }
            catch (Exception ex)
            {

            }
        }

        public override void STOP()
        {
            lock (_locker_stopping)
            {
                _cancelQueueTokenSource.Cancel();
                _ifReady.Set();
                _proceedingThread.Join();
            }
        }

        public override void Dispose()
        {
            STOP();

            lock (_locker_stopping)
            {
                _cancelQueueTokenSource.Dispose();

                _ifReady.Close();
                _ifReady.Dispose();
                _proceedMsg.Close();
                _proceedMsg.Dispose();
                _msg_queue = null;
            }
        }
    }



    public class MessageQueue_ProcessedByTimer : MessageQueueAbs
    {
        public MessageQueue_ProcessedByTimer(IEngine withEngine, Object data = null) : base(withEngine) { }

        private Object _locker = new Object();
        private Object _locker_stopping = new Object();
        private AutoResetEvent _ifReady = new AutoResetEvent(false);
        private ManualResetEvent _ifEnqueReady = new ManualResetEvent(false);
        private ManualResetEvent _ifDequeReady = new ManualResetEvent(false);

        //private AutoResetEvent _proceedMsg = new AutoResetEvent(false);
        private AutoResetEvent _finish_timer = new AutoResetEvent(false);

        //private MAK_MSG_proceed_method<T> _msg_proceed_method;
        private Queue<IPackage> _msg_queue = new Queue<IPackage>();
        private Thread _proceedingThread = null;
        private Boolean _enforceCancel = false;
        private CancellationTokenSource _cancelQueueTokenSource = new CancellationTokenSource();
        private CancellationToken _cancelQueueToken;

        //public MAK_MSG_proceed_method<T> MsgProceedMethod { get { return _msg_proceed_method; } }
        //public Thread ProceedingThread { get { return _proceedingThread; } }
        public Boolean EnforceCancel { get { return _enforceCancel; } set { _enforceCancel = value; } }

        private Int32 _timerSpeed = 40;
        private Timer _timer;

        public override void Enqueue(IPackage newMsg)
        {
            //lock (_locker)
            //{
                _ifEnqueReady.WaitOne();
                _ifDequeReady.Reset();
                lock (_msg_queue)
                {
                    _msg_queue.Enqueue(newMsg);
                    _ifReady.Set();
                }
                _ifDequeReady.Set();
                //var s = _proceedingThread.ThreadState;                
            //}
        }

        public override void RUN()
        {
            //_proceedingThread = new Thread(ProceedQueue);
            //_proceedingThread.Name = "SERVER_MSG_PROCEEDING";
            //_proceedingThread.Start();
            _ifEnqueReady.Set();
            _ifDequeReady.Set();
            //_timer = new Timer(ProceedQueue, _ifReady, 0, 1000);
            _cancelQueueToken = _cancelQueueTokenSource.Token;
            _timer = new Timer(ProceedQueue, null, 0, _timerSpeed);

            //_finish_timer.WaitOne();
        }

        private void ProceedQueue(Object state)
        {
            IPackage msg = null;
            List<IPackage> recieved_packages_batch = new List<IPackage>();

            //lock (_locker)
            //{
                if (_cancelQueueToken.IsCancellationRequested)
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    _finish_timer.Reset();
                    return;
                }

            //_ifReady.WaitOne();

                if (_msg_queue.Count == 0)
                {
                    Console.WriteLine("GameRoom:  0 in Queue..");
                    return;
                }
                _ifDequeReady.WaitOne();
                _ifEnqueReady.Reset();
                while (_msg_queue.Count > 0)
                {
                    msg = _msg_queue.Dequeue();
                    recieved_packages_batch.Add(msg);
                }
                _ifEnqueReady.Set();
            //}

            if (recieved_packages_batch != null)
            {
                Console.WriteLine("GameRoom  get from queue" + recieved_packages_batch.Count + " packages..");
                _serverEngine.ProcessMessages(recieved_packages_batch);
            }
            //else
            //    _ifReady.WaitOne();

        }


        public override void STOP()
        {
            lock (_locker_stopping)
            {
                _cancelQueueTokenSource.Cancel();
                _enforceCancel = true;
                //_proceedingThread.Join();
                _timer.Dispose(_finish_timer);
                _finish_timer.WaitOne();

            }
        }


        public override void Dispose()
        {
            STOP();

            lock (_locker_stopping)
            {
                _cancelQueueTokenSource.Dispose();

                _finish_timer.Close();
                _finish_timer.Dispose();

                _ifReady.Close();
                _ifReady.Dispose();

                _ifEnqueReady.Close();
                _ifEnqueReady.Dispose();

                _ifDequeReady.Close();
                _ifDequeReady.Dispose();

                _msg_queue = null;
            }
        }
    }


    public class MessageQueueFabric : IMessageQueueFabric
    {
        public IMessageQueue CreateMessageQueue(MsgQueueType queueType, IEngine withEngine = null)
        {
            IMessageQueue instance = null;

            switch (queueType)
            {
                case MsgQueueType.mqByTimerProcc:
                    instance = new MessageQueue_ProcessedByTimer(withEngine);
                    break;
                case MsgQueueType.mqOneByOneProcc:
                default:
                    instance = new MessageQueue_ProcessedOneByOne(withEngine);
                    break;
            }

            return instance;
        }
    }

}
