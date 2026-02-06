using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCore
{
    public class RecvBuffer
    {
        private ArraySegment<byte> m_Buffer;
        private int m_iReadPos;
        private int m_iWritePos;

        public RecvBuffer(int _iBufferSize)
        {
            m_Buffer = new ArraySegment<byte>(new byte[_iBufferSize], 0 ,_iBufferSize);
        }

        public int DataSize { get { return m_iWritePos - m_iReadPos; } }
        public int FreeSize { get { return m_Buffer.Count - m_iWritePos; } }

        public ArraySegment<byte> ReadSegment
        {
            //[r][][w][][][]
            get { return new ArraySegment<byte>(m_Buffer.Array, m_Buffer.Offset + m_iReadPos, DataSize); } 
        }

        //다음에 recv를 할때 어디서부터 어디까지 할지
        public ArraySegment<byte> WriteSegment
        {
            get { return new ArraySegment<byte>(m_Buffer.Array, m_Buffer.Offset + m_iWritePos, FreeSize); }
        }

        public void Clear()
        {
            int iDataSize = DataSize;
            if(iDataSize == 0)//r w 포인터가 같다면
            {
                m_iReadPos = 0;
                m_iWritePos = 0;
            }
            else
            {
                //남은 데이터를 앞으로 땡기기
                Array.Copy(m_Buffer.Array, m_Buffer.Offset + m_iReadPos, m_Buffer.Array, m_Buffer.Offset, iDataSize);
                m_iReadPos = 0;
                m_iWritePos = 0;
            }
        }


        //데이터를 성공적으로 읽었다면 커서 옮기기
        public bool OnRead(int _iNumByte)
        {
            if (_iNumByte > DataSize)
                return false;

            m_iReadPos += _iNumByte;
            return true;
        }

        public bool OnWrite(int _iNumByte)
        {
            if (_iNumByte > FreeSize)
                return false;

            m_iWritePos += _iNumByte;
            return true;
        }

    }
}
