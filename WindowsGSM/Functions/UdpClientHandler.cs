﻿using System;
using System.Collections.Generic;   
using System.Net;
using System.Net.Sockets;

namespace WindowsGSM.Functions
{
    /// <summary>
    /// UDP client utils
    /// </summary>
    public sealed class UdpClientHandler : IDisposable
    {
        private IPEndPoint _endPoint;
        private readonly UdpClient _udpClient;
        private bool _disposed;

        /// <summary>
        /// Base constructor.
        /// </summary>
        /// <param name="endPoint">IP and port of the remote PC.</param>
        /// <param name="sendTimeout">Send timeout.</param>
        /// <param name="receiveTimeout">Recieve timeout.</param>
        public UdpClientHandler(IPEndPoint endPoint)
        {
            this._endPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            this._udpClient = new UdpClient();
            this._udpClient.Connect(_endPoint);
        }

        /// <summary>
        /// Send your data then get the response data from the remote PC.
        /// </summary>
        /// <param name="requestData">Your data</param>
        /// <param name="length">Data length</param>
        /// <returns></returns>
        public IEnumerable<byte> GetResponse(IEnumerable<byte> requestData, int length, int sendTimeout, int receiveTimeout)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UdpClient));
            }
            else if(sendTimeout <= 0)
            {
                throw new ArgumentException($"{nameof(sendTimeout)} must be more than zero.");
            }
            else if(receiveTimeout <= 0)
            {
                throw new ArgumentException($"{nameof(receiveTimeout)} must be more than zero.");
            }

            _udpClient.Client.SendTimeout = sendTimeout;
            _udpClient.Client.ReceiveTimeout = receiveTimeout;
            _udpClient.Send([.. requestData], length);
#if DEBUG
            Console.WriteLine($"Started GetResponse from address {_endPoint.Address} and port {_endPoint.Port}");
#endif
            byte[] response = _udpClient.Receive(ref _endPoint);
#if DEBUG
            Console.WriteLine($"Got Data from UDP client: {string.Join(",", response)} , asci: {string.Join(",", response.Select((b) => (char)b))}");
#endif

            return response;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if(disposing)
            {
                // if you need that
            }

            _udpClient.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// If you forget to invoke dispose GC will do it for you
        /// </summary>
        ~UdpClientHandler()
        {
            Dispose(false);
        }
    }
}
