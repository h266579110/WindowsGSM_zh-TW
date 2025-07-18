﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;

namespace WindowsGSM.GameServer.Query
{
    public class FIVEM : IQueryTemplate
    {
        private static readonly byte[] FIVEM_INFO = Encoding.Default.GetBytes("getinfo windowsgsm");

        private IPEndPoint _IPEndPoint;
        private int _timeout;

        public FIVEM() { }

        public FIVEM(string address, int port, int timeout = 5)
        {
            SetAddressPort(address, port, timeout);
        }

        public void SetAddressPort(string address, int port, int timeout = 5)
        {
            _IPEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _timeout = timeout * 1000;
        }

        /// <summary>Get general information of specific game server.</summary>
        public async Task<Dictionary<string, string>> GetInfo()
        {
            return await Task.Run(() =>
            {
                try
                {
                    byte[] requestData;
                    byte[] responseData;
                    using (UdpClientHandler udpHandler = new(_IPEndPoint))
                    {
                        // Send FIVEM_INFO request
                        requestData = [0xFF, 0xFF, 0xFF, 0xFF, .. FIVEM_INFO];

                        // Receive response (Skip "\FF\FF\FF\FFinfoResponse\n\\")
                        responseData = [.. udpHandler.GetResponse(requestData, requestData.Length, _timeout, _timeout).Skip(18)];
                    }

                    string[] splits = Encoding.UTF8.GetString(responseData).Split('\\');

                    // Store response's data
                    Dictionary<string, string> keys = [];
                    for (int i = 0; i < splits.Length; i += 2)
                    {
                        keys.Add(splits[i], splits[i + 1]);
                    }

                    return keys.Count <= 0 ? null : keys;
                }
                catch
                {
                    return null;
                }
            });
        }

        public async Task<string> GetPlayersAndMaxPlayers()
        {
            try
            {
                Dictionary<string, string> kv = await GetInfo();
                return kv["clients"] + '/' + kv["sv_maxclients"];
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<PlayerData>> GetPlayersData()
        {
            return null;
        }
    }
}
