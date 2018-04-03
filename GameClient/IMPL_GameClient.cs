﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tanki
{
    public class GameClient:NetProcessorAbs,IGameClient
    {
        //взять этот за основу
        public GameClient(IPEndPoint localEP, IRoomOwner owner) : base("", localEP, owner)
        {
            IReciever _Reciever = new ReceiverUdpClientBased(localEP);
            base.RegisterDependcy(_Reciever);

            Sender = new SenderUdpClientBased(Reciever);
            
            // Нужно будет прописать создание клиентского Engine
            //IEngine _Engine = (new ServerEngineFabric()).CreateEngine(SrvEngineType.srvManageEngine);
            //base.RegisterDependcy(_Engine);

            IMessageQueue _MessageQueue = (new MessageQueueFabric()).CreateMessageQueue(MsgQueueType.mqOneByOneProcc);
            base.RegisterDependcy(_MessageQueue);

        }


        // должен быть приватный TCPClien  для коннекта к хосту

        //должен быть приватный Dictionary<String, IAddresssee>  для хранения перечня адрессатов

        //должен быть приватный Timer - на callBack которого будет вызываться метод переодической отправки клинтского состояния игры на сервер.

        //должен будет быть приватный метод  'void ProceedQueue(Object state)' который будет передаваться time-ру как callback 
                                                // этот метод должен с периодиностью таймера отправлять клиентское состояние игры на сервер
    }
}