using DummyClient;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class PacketHandler
{
    public static void S_ChatHandler(PacketSession _refSession, IPacket _iPacket)
    {
        S_Chat chatPacket = _iPacket as S_Chat;
        ServerSession serverSession = _refSession as ServerSession;

        if(chatPacket.playerID == 1)
            Console.WriteLine(chatPacket.chat);
    }
}

