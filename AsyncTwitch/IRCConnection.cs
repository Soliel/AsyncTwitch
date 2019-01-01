using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace AsyncTwitch
{
    public abstract class IrcConnection : MonoBehaviour
    {
        /*
         * Everyone say thanks to Umbranoxio for all his help with this!
         */
        #region Private Vars
        private const int BUFFER_SIZE = 8192;
        private const int RECONNECT_LIMIT = 20;
        private readonly byte[] EOF = new byte[] { 13, 10};

        private byte[] _buffer = new byte[BUFFER_SIZE];
        private Socket _twitchSocket;
        private Queue<byte[]> _receivedQueue = new Queue<byte[]>();

        private readonly object _readLock = new object();
        private bool _reading;
        protected int _reconnectCount;
        private bool _firstConnection = true;
        private string _storedhost;
        private ushort _storedport;
        private Logger _logger;
        #endregion

        public abstract void OnConnect();
        public abstract void ProcessMessage(byte[] msg);


        internal void Connect(string host, ushort port)
        {
            if (_logger == null)
            {
                _logger = LogManager.GetCurrentClassLogger();
            }

            _twitchSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _twitchSocket.BeginConnect(host, port, ConnectCallback, null);
            _logger.Info($"Beginning connection to server {host}:{port}");
            _storedhost = host;
            _storedport = port; 
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            _twitchSocket.EndConnect(ar);
            if (!_twitchSocket.Connected)
            {
                if (_reconnectCount > RECONNECT_LIMIT)
                {
                    _reconnectCount = 0;
                    _logger.Info("Socket reconnect limit reached. Aborting.");
                    return;
                }
                _logger.Info("Socket failed to connect to server retrying.");
                if (_firstConnection)
                    _firstConnection = false;
                else
                    _reconnectCount++;
                Connect(_storedhost, _storedport);
            }
            _logger.Info("Connected! Beginning to receive data.");
            _twitchSocket.BeginReceive(_buffer, 0, BUFFER_SIZE, SocketFlags.None, new AsyncCallback(Receive), null);
            OnConnect();
        }

        private void Receive(IAsyncResult ar)
        {
            int byteLength;
            try
            {
                byteLength = _twitchSocket.EndReceive(ar);
                if (byteLength <= 0)
                {
                    Disconnect();
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

                Disconnect();
                _logger.Debug($"Unable to recieve data. Aborting with error: {e}");
                return;
            }

            _logger.Trace($"Received {byteLength} bytes of data.");
            byte[] receivedBytes = new byte[byteLength];

            //Copy can fail so we wrap in a try catch. If it does our network data isn't worth looking at anymore so we reconnect.
            try
            {
                _logger.Trace("Attempting to copy from global buffer.");
                Array.Copy(_buffer, receivedBytes, receivedBytes.Length); //Free up our buffer as fast as possible.
            }
            catch (Exception e)
            {
                Disconnect();
                _logger.Debug($"Unable to copy to global buffer with error: {e}");
                return;
            }

            //Queue our bytes to send to another thread. Lock the queue for thread safety.
            lock (_receivedQueue)
            {
                _logger.Trace("Queueing data.");
                _receivedQueue.Enqueue(receivedBytes);
            }

            //lock readlock while we queue the process. 
            lock (_readLock)
            {
                if (!_reading)
                {
                    _logger.Info("Queueing a new ProcessReceived task.");
                    _reading = true;
                    ThreadPool.QueueUserWorkItem(ProcessReceived);
                }
            }

            _twitchSocket.BeginReceive(_buffer, 0, BUFFER_SIZE, SocketFlags.None, Receive, null);
        }

        internal void Send(string data)
        {
            if (!_twitchSocket.Connected) return;
            _logger.Info("Sending message.");
            List<byte> byteData = new List<byte>(Encoding.ASCII.GetBytes(data));
            byteData.AddRange(EOF);
            _twitchSocket.BeginSend(byteData.ToArray(), 0, byteData.Count, 0, SendCallback, null);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                _twitchSocket.EndSend(ar);
                _logger.Info("Sending completed.");
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send data with error: {e}");
            }
        }

        //Loops through our data until empty.
        private void ProcessReceived(object obj)
        {
            byte[] readBuffer = new byte[BUFFER_SIZE];
            byte[] nextBuffer = { };

            int readBytes = 0;
            int nextBytes = 0;
            int loadBytes = 0;

            while (true)
            {
                if (nextBytes == 0 && readBytes == 0)
                {
                    lock (_receivedQueue)
                    {
                        if (_receivedQueue.Count == 0)
                        {
                            _reading = false;
                            break;
                        }

                        _logger.Trace("Attempting dequeue.");
                        nextBytes = _receivedQueue.Peek().Length;
                        nextBuffer = _receivedQueue.Dequeue();
                    }
                }

                if (readBytes < BUFFER_SIZE && nextBytes != 0)
                {
                    loadBytes = BUFFER_SIZE - readBytes;

                    if (loadBytes > nextBuffer.Length)
                    {
                        loadBytes = nextBuffer.Length;
                    }

                    try
                    {
                        Array.Copy(nextBuffer, 0, readBuffer, readBytes, loadBytes);
                        nextBytes -= loadBytes;
                        readBytes += loadBytes;
                    }
                    catch(Exception e)
                    {
                        _logger.Error(e.ToString());
                        Disconnect();
                        break;
                    }
                }
                if (readBytes == 0) continue; //If readBytes has no data we wanna skip this loop.

                //By this point readBuffer should have some data we can operate on.
                int offset = FindBytePattern(readBuffer, EOF, 0);
                if (offset == -1)
                {
                    _logger.Debug("Found a malformed packet.");
                    Disconnect();
                    break;
                }
                byte[] processedData = new byte[offset]; //Set the length to offset + 1 to contain a full message.

                try
                {
                    Array.Copy(readBuffer, 0, processedData, 0, offset); //Copy till EOF 
                }
                catch(Exception e)
                {
                    _logger.Error(e.ToString());
                    Disconnect();
                    break;
                }

                //processed Data now holds a single IRC message so we will pass that into another function.
                _logger.Trace("Moving into subclass.");
                ProcessMessage(processedData);

                //Time to clean up
                try
                {
                    //This copy moves our readBuffer forward. This is basically a queue. Offset + 2 is to get rid of the ending CR+LF.
                    Array.Copy(readBuffer, offset + 2, readBuffer, 0, readBytes - (offset + 2));
                    readBytes -= (offset + 2);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                    Disconnect();
                    break;
                }
            }
        }

        public void Disconnect()
        {
            _reading = false;
            _twitchSocket.BeginDisconnect(true, DisconnectCallback, null);
        }

        public void DisconnectCallback(IAsyncResult ar)
        {
            _twitchSocket.EndDisconnect(ar);
            _reconnectCount++;
            Thread.Sleep(_reconnectCount * 500);
            Connect(_storedhost, _storedport);
        }

        //This is really fast for how simple it is.
        public int FindBytePattern(byte[] source, byte[] search, int offset)
        {
            int searchLimit = (source.Length - offset) - search.Length; //If we haven't found a match by this point we wont.

            for (int i = offset; i <= searchLimit; i++)
            {
                int x = 0;
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
