using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace ServerCore
{
    //샌드버퍼를 세션 내부에서 들고있으면 성능적 문제가 있음
    //외부에서 만들어진 패킷의 데이터를 멤버 변수에 배열에 복사하기 보다는
    //외부에서 만들어진 버퍼를 만들어준 뒤 데이터를 해당 버페에 넣고 전송하는게 효율적

    public class SendBufferHelper
    {
        //스레드끼리 경합을 줄이기 위해서 스레드 로컬로
        public static ThreadLocal<SendBuffer> m_refCurBuffer = new ThreadLocal<SendBuffer>(() => { return null; });

        public static int ChunckSize { get; set; } = 4096 * 100;

        public static ArraySegment<byte> Open(int _iReserveSize)
        {
            if(m_refCurBuffer.Value == null)
                m_refCurBuffer.Value = new SendBuffer(ChunckSize);

            if (m_refCurBuffer.Value.FreeSize < _iReserveSize)
                m_refCurBuffer.Value = new SendBuffer(ChunckSize);

            return m_refCurBuffer.Value.Open(_iReserveSize);
        }

        public static ArraySegment<byte> Close(int _iUseSize)
        {
            return m_refCurBuffer.Value.Close(_iUseSize);
        }
    }

    public class SendBuffer
    {

        private byte[] m_Buffer;
        private int m_iUseSize;

        public SendBuffer(int _iChunkSize)
        {
            m_Buffer = new byte[_iChunkSize];
        }
        public int FreeSize 
        {
            get { return m_Buffer.Length - m_iUseSize; }
        }

        //내 바이트에서 할당하는 만큼 던져주기
        public ArraySegment<byte> Open(int _iReserveSize)
        {
            if (_iReserveSize > FreeSize)
                return null;

            return new ArraySegment<byte>(m_Buffer, m_iUseSize, _iReserveSize);
        }
        //버퍼를 다 썼다고 반환할 때
        public ArraySegment<byte> Close(int _iUseSize)
        {
            //내가 사용한 범위만큼 옮긴 후 배열 반환
            ArraySegment<byte> arrUseSeg = new ArraySegment<byte>(m_Buffer, m_iUseSize, _iUseSize);
            m_iUseSize += _iUseSize;
            return arrUseSeg;
        }
    }
}
