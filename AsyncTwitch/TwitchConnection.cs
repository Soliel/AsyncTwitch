using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AsyncTwitch
{
    public class TwitchConnection
    {
        #region Global Vars
        private const int BUFFER_SIZE = 8192;
        private readonly byte[] EOF = new byte[] { 13, 10};

        private byte[] _buffer = new byte[BUFFER_SIZE];
        private Socket _twitchSocket;
        private Queue<byte[]> _recievedQueue = new Queue<byte[]>();

        private Object _readLock = null;
        private bool _reading = false;
        private byte[] _processedBuffer;
        private bool _readState;

        private int writeOffset;
        private int readOffset;
        private int readableLength;
        #endregion

        public void Connect(string host, ushort port)
        {
            _twitchSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _twitchSocket.BeginConnect(host, port, new AsyncCallback(ConnectCallback), null);
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            _twitchSocket.EndConnect(ar);
            if(!_twitchSocket.Connected) return;
            _twitchSocket.BeginReceive(_buffer, 0, BUFFER_SIZE, SocketFlags.None, new AsyncCallback(Recieve), null);
        }

        private void Recieve(IAsyncResult ar)
        {
            int byteLength;
            try
            {
                byteLength = _twitchSocket.EndReceive(ar);
                if (byteLength <= 0)
                {
                    //Disconnect
                    return;
                }
            }
            catch (Exception e)
            {
                //Treat NRE and ODE nicer.
                if (e is NullReferenceException || e is ObjectDisposedException)
                {
                    return;
                }
                //Disconnect here
                return;
            }

            var recievedBytes = new byte[byteLength];

            //UMBRA WHY
            try
            {
                Array.Copy(_buffer, recievedBytes, recievedBytes.Length);
            }
            catch (Exception)
            {
                //Disconnect
                return;
            }

            //Queue our bytes to send to another thread.
            lock(_recievedQueue)
                _recievedQueue.Enqueue(recievedBytes);

            //Send bytes to another thread.
            lock (_readLock)
            {
                if (!_reading)
                {
                    _reading = true;
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessReceived));
                }
            }

            _twitchSocket.BeginReceive(_buffer, 0, BUFFER_SIZE, SocketFlags.None, new AsyncCallback(Recieve), null);
        }

        /*
         * WARNING VOODOO AHEAD
         */
        private void ProcessReceived(object obj)
        {
            while (true)
            {
                byte[] readBuffer;
                lock (_recievedQueue)
                {
                    if (_recievedQueue.Count == 0)
                    {
                        _reading = false;
                        return;
                    }

                    readBuffer = _recievedQueue.Dequeue();
                }

                readableLength += readBuffer.Length;
                bool process = true;
                while (process)
                {
                    if (_readState)
                    {
                        var offset = FindBytePattern(readBuffer, EOF, readOffset);
                        if (_processedBuffer == null || _processedBuffer.Length != offset)
                        {
                            _processedBuffer = new byte[offset];
                        }

                        int length = (writeOffset + readableLength >= offset)
                            ? offset - writeOffset
                            : readableLength;

                        try
                        {
                            Array.Copy(readBuffer, readOffset, _processedBuffer, writeOffset, length);
                        }
                        catch
                        {
                            process = false;
                            //disconnect
                            break;
                        }

                        writeOffset += length;
                        readOffset += length;
                        readableLength -= length;

                        if (writeOffset == offset)
                        {
                            if (_processedBuffer.Length == 0)
                            {
                                process = false;
                                //disconnect
                                break;
                            }

                            //PacketComplete(_processedBuffer)
                            _processedBuffer = null;
                            writeOffset = 0;
                        }
                        if (readableLength == 0)
                        {
                            process = false;
                            _readState = false;
                        }
                        break;
                    }
                    else
                    {
                        _readState = true;
                    }
                }
                if (!_readState)
                {
                    writeOffset = 0;
                    readOffset = 0;
                    readableLength = 0;
                }
            }
        }

        //This is really fast for how simple it is.
        private int FindBytePattern(byte[] source, byte[] search, int offset)
        {
            var searchLimit = (source.Length - offset) - search.Length; //If we haven't found a match by this point we wont.

            for (var i = offset; i <= searchLimit; i++)
            {
                var x = 0;
                for (; x < search.Length; x++) //Iterate through the array after index i until we fully match search or find a difference.
                {
                    if (search[x] != source[i + x]) break;
                }

                if (x == search.Length) return i;
            }

            return -1;
        }
    }
}
