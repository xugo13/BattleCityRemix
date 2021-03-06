﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tanki
{
    public class GameServer: ListeningClientAbs,  IServer
    {
        private GameServer() { }

        //public GameServer(IListener listener, ISystemSettings sysSettings, IRoomFabric RoomFabric = null, IServerEngineFabric EngineFabric = null)
        public GameServer(IIpEPprovider ipEpProvider, ISystemSettings sysSettings, IRoomFabric RoomFabric = null, IServerEngineFabric EngineFabric = null)
        {
            _sys_settings = sysSettings;
            _next_room_port = sysSettings.RoomPortMin;

            ServerListner = new Listener(ipEpProvider,sysSettings.HostListeningPort);
            RegisterListener(ServerListner);

            if (RoomFabric != null)
                _roomFabric = RoomFabric;
            else
                _roomFabric = new RoomFabric();

            if (EngineFabric != null)
                _engineFabric = EngineFabric;
            else
                _engineFabric = new ServerEngineFabric();



        }

        private IRoomFabric _roomFabric;
        private IServerEngineFabric _engineFabric;


        private List<IRoom> _rooms = new List<IRoom>();
        private ISystemSettings _sys_settings;
        //private IEngine _mngEngine;
        //private IEngine _gameEngine;
        private Int32 _next_room_port;

        public IListener ServerListner { get; private set; }
        public IEnumerable<IRoom> Rooms { get { return _rooms; } }

        public override void OnNewConnectionHandler(object Sender, NewConnectionData evntData)
        {
            var remoteEP = (IPEndPoint)evntData.RemoteClientSocket.RemoteEndPoint;
            Gamer newGamer = new Gamer(remoteEP);           
            _rooms[0].AddGamer(newGamer);

            // у Room будет событие OnNewGamer
            // для Управляющей комнаты по этому событию будет отправка клиенту IPackage.Data = Gamer.GUID
        }

        public void RUN()
        {
            IPAddress roomAddr = ((IPEndPoint)ServerListner.ipv4_listener.LocalEndPoint).Address;
            Int32 roomPort = GetNextRoomPort();
            IPEndPoint roomEP = new IPEndPoint(roomAddr, roomPort);

            IEngine _mngEngine = _engineFabric.CreateEngine(SrvEngineType.srvManageEngine);
            IRoom managerRoom = _roomFabric.CreateRoom("", roomEP, RoomType.rtMngRoom, this, _mngEngine);
            _rooms.Add(managerRoom);

            managerRoom.RUN();

            ServerListner.RUN();
        }

        public IEnumerable<IRoomStat> getRoomsStat()
        {
            //var rSts = from r in Rooms select new RoomStat(){
            //    Pasport = r.Passport,
            //    Players_count = r.Gamers.Count(),
            //    /*Creator_Pasport = r.CreatorPassport*/                 

            //};

            var rSts = from r in Rooms where r.Room_Type == RoomType.rtGameRoom select r.getRoomStat();

            return rSts;
        }

        public IPEndPoint MooveGamerToRoom(IGamer gamer, Guid TargetRoomId)
        {
            IRoom targetRoom;

            var selRoom = from r in Rooms where r.Passport == TargetRoomId select r;
            targetRoom = selRoom.First();

            try
            {
                targetRoom.AddGamer(gamer);
                return targetRoom.Reciever.LockalEndPoint;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public IRoomStat getRoomStat(String RoomID)
        {
            IRoomStat res = null;
            var selRooms = from r in Rooms where r.RoomId == RoomID select r;
            IRoom selRoom = selRooms.FirstOrDefault();

            if (selRoom != null) res = selRoom.getRoomStat();

            return res;

            //return new RoomStat() {
            //    Pasport = selRoom.Passport,
            //    Players_count = selRoom.Gamers.Count(),
            //    /*Creator_Pasport = selRoom.CreatorPassport*/
            //};


        }

        public IRoom GetRoomByGuid(Guid roomGuid)
        {
            IRoom foundRoom = null;

            var r = (from R in Rooms where R.Passport == roomGuid select  R);            
            if (r.Count() > 1) throw new Exception("Rooms ID not unique");

            foundRoom = r.FirstOrDefault();

            return foundRoom;
        }

        Int32 GetNextRoomPort()
        {
            if (_next_room_port > _sys_settings.RoomPortMax) throw new Exception("RoomPortMax is exceeded");
            return _next_room_port++;
        }

        public IRoom AddRoom(IGameSetings gameSettings, Guid Creator_Passport)
        {
            IPAddress roomAddr = ((IPEndPoint)ServerListner.ipv4_listener.LocalEndPoint).Address;
            Int32 roomPort = GetNextRoomPort();

            IEngine _gameEngine = _engineFabric.CreateEngine(SrvEngineType.srvGameEngine);
            IRoom newGameRoom = _roomFabric.CreateRoom("",new IPEndPoint(roomAddr,roomPort), RoomType.rtGameRoom, this ,_gameEngine);
            newGameRoom.GameSetings = gameSettings;
            newGameRoom.CreatorPassport = Creator_Passport;

            _rooms.Add(newGameRoom);
            //newGameRoom.RUN();

            return newGameRoom;
        }

        public void RemoveGamerFromRoom(IGamer gamer, Guid TargetRoomId)
        {
            IRoom targetRoom;

            var selRoom = from r in Rooms where r.Passport == TargetRoomId select r;
            targetRoom = selRoom.First();

            try
            {
                targetRoom.AddGamer(gamer);                
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void RemoveRoom(IRoom room2remove)
        {
            _rooms.Remove(room2remove);
            room2remove.STOP();
            room2remove.Dispose();
        }
    }
}
