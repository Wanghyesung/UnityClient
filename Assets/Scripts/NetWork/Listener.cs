using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerCore
{
    public class Listener
    {
        private Socket m_ListenSoket = null;
        private Func<Session> m_refSessionHandler = null; //세션을 어떤 방식으로 만들어줄지

        public void Init(IPEndPoint _refEndPoint, Func<Session> _refSessionHandler)
        {
            m_ListenSoket = new Socket(_refEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            m_refSessionHandler += _refSessionHandler;

            m_ListenSoket.Bind(_refEndPoint);
            m_ListenSoket.Listen(10); //최대 대기 수

            //완료되면 콜백 함수
            for(int i = 0; i<10; ++i)
            {
                SocketAsyncEventArgs refArgs = new SocketAsyncEventArgs();
                refArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
                RegisterAccept(refArgs);
            }   
        }

        private void RegisterAccept(SocketAsyncEventArgs _refArgs)
        {
            //기존에 이벤트가 계속 남아있을 수 있기 때문에 null로 밀고 시작
            _refArgs.AcceptSocket = null;

            bool bPending = m_ListenSoket.AcceptAsync(_refArgs);
            if(bPending == false)
            {
                //실행하자마자 된다면
                OnAcceptCompleted(null, _refArgs);
            }

        }

        //스레풀에서 알아서 스레드를 하나 가져와서 실행
        private void OnAcceptCompleted(object _oSender, SocketAsyncEventArgs _refArgs)
        {
            if (_refArgs.SocketError == SocketError.Success)
            {
                Session refGameSession = m_refSessionHandler.Invoke();
                refGameSession.Start(_refArgs.AcceptSocket);//accept한 클라 소켓
                refGameSession.OnConnected(_refArgs.AcceptSocket.RemoteEndPoint);
            }
            else
                Console.Write(_refArgs.SocketError.ToString());

            RegisterAccept(_refArgs);
        }
    }
}
