using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
namespace ServerCore
{
    public abstract class PacketSession : Session
    {
        public static readonly int HeaderSize = 2;

        public sealed override int OnRecv(ArraySegment<byte> _arrBuffer)
        {
            int iPorcessLen = 0;

            while(true)
            {
                //최소한 헤더
                if (_arrBuffer.Count < HeaderSize)
                    break;

                //패킷이 완천체로 도착했는지 확인(데이터 사이즈 확인, 데이터 크기만큼 버퍼 크기가 필요)
                ushort sDatasize = BitConverter.ToUInt16(_arrBuffer.Array, _arrBuffer.Offset);
                if (_arrBuffer.Count < sDatasize)
                    break;

                //스택에 복사
                OnRecvPacket(new ArraySegment<byte>(_arrBuffer.Array, _arrBuffer.Offset, sDatasize));

                //다음 패킷이 있는 장소로 이동
                iPorcessLen += sDatasize;
                _arrBuffer = new ArraySegment<byte>(_arrBuffer.Array, _arrBuffer.Offset + sDatasize, _arrBuffer.Count - sDatasize);
            }

            return iPorcessLen;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> _arrBuffer);
    }

    
    public abstract class Session
    {
        private Socket m_refSocket = null;
        private int m_iDisConnected = 0;

        RecvBuffer m_refRecvBuffer = new RecvBuffer(1024);
        private object m_lock = new object();
        private Queue<ArraySegment<byte>> m_qSendQeueu = new Queue<ArraySegment<byte>>();
        List<ArraySegment<byte>> m_listPending = new List<ArraySegment<byte>>();
      
        //다른 스레드에서 sned를 하는동안 또 다른 스레드에서 센드를 한다면 공유자원ARGS에 따라
        //예상치못한 오류가 발생핳 수 있음

        private SocketAsyncEventArgs m_refSendArgs = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs m_refRecvArgs = new SocketAsyncEventArgs();

        public abstract void OnConnected(EndPoint _refEndPoint);
        public abstract int OnRecv(ArraySegment<byte> _arrBuffer);
      
        public abstract void OnSend(int _iBytes);
        public abstract void OnDisConnected(EndPoint _refEndPoint);

        void Clear()
        {
            lock(m_lock)
            {
                m_qSendQeueu.Clear();
                m_listPending.Clear();
            }
        }

        public void Start(Socket _refSocket)
        {
            m_refSocket = _refSocket;
         
            //한 클라 소켓의 recv 콜백은 순차적으로 온다(동시 실행 X)
            //하지만 그 콜백을 실행하는 스레드는 고정이 아니다
            m_refRecvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
            //refRecvArgs.UserToken = 식별자나 연동하고싶은 데이터를 넣는 자리
            //recv버퍼 생성
            //m_refRecvArgs.SetBuffer(new byte[1024], 0, 1024); //recv버퍼 큰걸로 잡고 쪼개서 사용가능(offset 존재 이유)


            m_refSendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompledted);

            RegisterRecv();
        }

        public void Send(ArraySegment<byte> sendBuffer)
        {
            lock(m_lock)
            {
                m_qSendQeueu.Enqueue(sendBuffer);
                if (m_listPending.Count == 0)
                    RegiserSend();
            }
        }

        public void DisConnect()
        {
            //멀티스레드 환경에서 연결해제가 동시에 2번 들어올 때 문제가 생길 수 있기 때문에 atomic 사용
            //1로 바꾸고 기존의 값을 반환해준다 . return 0
            if (Interlocked.Exchange(ref m_iDisConnected, 1) == 1)
                return;

            OnDisConnected(m_refSocket.RemoteEndPoint);
            m_refSocket.Shutdown(SocketShutdown.Both);
            m_refSocket.Close();
            Clear();
        }

        void RegiserSend()
        {
            if (m_iDisConnected == 1)
                return;

            while (m_qSendQeueu.Count > 0)
            {
                ArraySegment<byte> arrBuff = m_qSendQeueu.Dequeue();
                //값복사
                //TCP 특성상 한번에 데이터가 다 들오지 않을 수 있음 때문에 입번 프레임에는 어디까지 왔고를 offset으로 기록해야함
                m_listPending.Add(arrBuff);
            }

            //클라에서 끊는 타이밍에 내 소켓이 보내려함
            try
            {
                //리스트를 만들고 연결해야함
                m_refSendArgs.BufferList = m_listPending;

                bool bPending = m_refSocket.SendAsync(m_refSendArgs);
                if (bPending == false)
                    OnSendCompledted(null, m_refSendArgs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        //이벤트 콜백으로 다른 스레드에서 동시에 접근 가능
        void OnSendCompledted(object _oSender, SocketAsyncEventArgs _refArgs)
        {
            lock(m_lock)
            {
                if (_refArgs.BytesTransferred > 0 && _refArgs.SocketError == SocketError.Success)
                {
                    try
                    {
                        _refArgs.BufferList = null;
                        m_listPending.Clear();

                        OnSend(_refArgs.BytesTransferred);

                        //Console.WriteLine()
                        //내가 샌드를 하는동안 다른 스레드가 에약을 해놨다면 내가 처리
                        if(m_qSendQeueu.Count > 0)
                            RegiserSend();
                       

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
                else
                {
                    DisConnect();
                }
            }
           
        }

        void RegisterRecv()
        {
            if (m_iDisConnected == 1)
                return;


           m_refRecvBuffer.Clear();
           //현재 쓸수있는구간의 버퍼 전달

           ArraySegment<byte> arrWriteSeg = m_refRecvBuffer.WriteSegment;
           m_refRecvArgs.SetBuffer(arrWriteSeg.Array, arrWriteSeg.Offset, arrWriteSeg.Count);

            try
            {
                bool bPending = m_refSocket.ReceiveAsync(m_refRecvArgs);
                if (bPending == false)
                    OnRecvCompleted(null, m_refRecvArgs);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }

        }

        void OnRecvCompleted(object _oSender, SocketAsyncEventArgs _refArgs)
        {
            //전송받은 바이트 크기 (0은 연결 종료)
            if(_refArgs.BytesTransferred > 0 && _refArgs.SocketError == SocketError.Success)
            {
                try
                {
                    //Write 커서를 이동
                    if(m_refRecvBuffer.OnWrite(_refArgs.BytesTransferred) == false)
                    {
                        DisConnect();
                        return;
                    }

                    //컨텐츠쪽으로 데이터를 전달해주고 데이터를 얼마나 처리했는지 받는다

                    int iProcessLen = OnRecv(m_refRecvBuffer.ReadSegment); //얼마만큼 데이터를 처리했는지
                    if(iProcessLen < 0 || m_refRecvBuffer.DataSize < iProcessLen)
                    {
                        DisConnect();
                        return;
                    }
                    
                    //Read 커서 이동
                    if(m_refRecvBuffer.OnRead(iProcessLen) == false)
                    {
                        DisConnect();
                        return;
                    }

                    RegisterRecv();
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            else
            {
                //Disconnect
                DisConnect();
            }
        }
    }
}
