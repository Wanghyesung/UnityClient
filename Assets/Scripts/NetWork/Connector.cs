using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
namespace ServerCore
{
    //분산 서버를 구현할 시 다른 쪽 서버도 연결을 해야하기 때문 
    public class Connector
    {
        private Func<Session> m_refSessionHandler = null;
        public void Connect(IPEndPoint _refEndPoint, Func<Session> _refSessionHandler, int _iCount = 1)
        {
            for(int i = 0; i< _iCount; i++)
            {
                Socket refSocket = new Socket(_refEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                m_refSessionHandler = _refSessionHandler;

                SocketAsyncEventArgs refArgs = new SocketAsyncEventArgs();
                refArgs.Completed += OnConnectCompleted;
                refArgs.RemoteEndPoint = _refEndPoint; //상대방의 주소
                refArgs.UserToken = refSocket; //여러 서버와 연동할 수 있기 때문에 멤버변수 하나로 안하고 userToken에 넣기

                RegiserConnect(refArgs);
            }
        }

        private void RegiserConnect(SocketAsyncEventArgs _refArgs)
        {
            Socket refSocket = _refArgs.UserToken as Socket;
            //bool bPending = Socket.ConnectAsync(_refArgs);
            if (refSocket == null)
                return;

            bool bPending = refSocket.ConnectAsync(_refArgs);
            if (bPending == false)
                OnConnectCompleted(null, _refArgs);
        }

        private void OnConnectCompleted(object _oSender, SocketAsyncEventArgs _refArgs)
        {
            if(_refArgs.SocketError == SocketError.Success)
            {
                Session refSession = m_refSessionHandler.Invoke();
                refSession.Start(_refArgs.ConnectSocket);
                refSession.OnConnected(_refArgs.RemoteEndPoint);
            }
            else
            {

            }
        }
    }
}
