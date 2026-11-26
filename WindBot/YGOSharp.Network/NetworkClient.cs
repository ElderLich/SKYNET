using System;
using System.Net;
using System.Net.Sockets;

namespace YGOSharp.Network
{
    public class NetworkClient
    {
        public event Action Connected;
        public event Action<Exception> Disconnected;
        public event Action<byte[]> DataReceived;

        public bool IsConnected { get { return _isConnected; } }

        public IPAddress RemoteIPAddress
        {
            get { return _endPoint.Address; }
        }

        private const int BufferSize = 4096;

        private Socket _socket;
        private IPEndPoint _endPoint;
        private readonly object _stateLock = new object();
        private volatile bool _isConnected;
        private bool _isClosed;
        private byte[] _receiveBuffer = new byte[BufferSize];

        public NetworkClient()
        {
        }

        public NetworkClient(Socket socket)
        {
            Initialize(socket);
        }

        public void Initialize(Socket socket)
        {
            lock (_stateLock)
            {
                if (_isClosed)
                    throw new ObjectDisposedException("NetworkClient");
                if (_isConnected)
                    throw new InvalidOperationException("The client already owns a socket.");

                _endPoint = (IPEndPoint)socket.RemoteEndPoint;
                _socket = socket;
                _isConnected = true;
            }
            Connected?.Invoke();
        }

        public void BeginConnect(IPAddress address, int port)
        {
            try
            {
                lock (_stateLock)
                {
                    if (_isClosed || _isConnected)
                        return;

                    _endPoint = new IPEndPoint(address, port);
                    _socket = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _isConnected = true;
                }
                _socket.BeginConnect(_endPoint, new AsyncCallback(ConnectCallback), null);
            }
            catch (Exception ex)
            {
                Close(ex);
            }
        }

        public void BeginSend(byte[] data)
        {
            try
            {
                _socket.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, data.Length);
            }
            catch (Exception ex)
            {
                Close(ex);
            }
        }

        public void BeginReceive()
        {
            try
            {
                _socket.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ReceiveCallback, null);
            }
            catch (Exception ex)
            {
                Close(ex);
            }
        }

        public void Close(Exception error = null)
        {
            lock (_stateLock)
            {
                if (_isClosed)
                    return;

                _isClosed = true;
                _isConnected = false;
            }

            try
            {
                _socket?.Close();
            }
            catch (Exception ex)
            {
                error = error == null ? ex : new AggregateException(error, ex);
            }
            Disconnected?.Invoke(error);
        }

        private void ConnectCallback(IAsyncResult result)
        {
            try
            {
                _socket.EndConnect(result);
            }
            catch (Exception ex)
            {
                Close(ex);
                return;
            }
            Connected?.Invoke();
            BeginReceive();
        }

        private void SendCallback(IAsyncResult result)
        {
            try
            {
                int bytesSent = _socket.EndSend(result);
                if (bytesSent != (int)result.AsyncState)
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                Close(ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            int bytesRead;
            try
            {
                bytesRead = _socket.EndReceive(result);
            }
            catch (Exception ex)
            {
                Close(ex);
                return;
            }
            if (bytesRead == 0)
            {
                Close();
                return;
            }
            byte[] data = new byte[bytesRead];
            Array.Copy(_receiveBuffer, data, bytesRead);
            DataReceived?.Invoke(data);
            BeginReceive();
        }
    }
}
