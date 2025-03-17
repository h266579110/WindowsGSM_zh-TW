﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;

namespace WindowsGSM.GameServer.Query
{
    public class A2S: QueryTemplate
    {
        private static readonly byte[] A2S_INFO = Encoding.Default.GetBytes("TSource Engine Query\0");
        private static readonly byte[] A2S_PLAYER = Encoding.Default.GetBytes("U");
        private static readonly byte[] A2S_RULES = Encoding.Default.GetBytes("V");

        private const byte SOURCE_RESPONSE = 0x49;
        private const byte GOLDSOURCE_RESPONSE = 0x6D;

        private IPEndPoint _IPEndPoint;
        private int _timeout;

        public A2S() { }

        public A2S(string address, int port, int timeout = 5)
        {
            SetAddressPort(address, port, timeout);
        }

        public void SetAddressPort(string address, int port, int timeout = 5)
        {
            _IPEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _timeout = timeout * 1000;
        }

        /// <summary>Retrieves information about the server including, but not limited to: its name, the map currently being played, and the number of players.</summary>
        /// <returns>Returns (key, value)</returns>
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
                        requestData = [0xFF, 0xFF, 0xFF, 0xFF, .. A2S_INFO];

                        responseData = [.. udpHandler.GetResponse(requestData, requestData.Length, _timeout, _timeout).Skip(4)];

                        if (responseData[0] == 0x41)
                        {
                            requestData = [0xFF, 0xFF, 0xFF, 0xFF, .. A2S_INFO, .. responseData.Skip(1)];

                            responseData = [.. udpHandler.GetResponse(requestData, requestData.Length, _timeout, _timeout).Skip(4)];
                        }
                    }

                    // Store response's data
                    Dictionary<string, string> keys = [];

                    // Load response's data
                    using (BinaryReader br = new(new MemoryStream(responseData), Encoding.UTF8))
                    {
                        byte header = br.ReadByte();

                        if (header == SOURCE_RESPONSE)
                        {
                            keys["Header"] = header.ToString();
                            keys["Protocol"] = br.ReadByte().ToString();
                            keys["Name"] = ReadString(br);
                            keys["Map"] = ReadString(br);
                            keys["Folder"] = ReadString(br);
                            keys["Game"] = ReadString(br);
                            keys["ID"] = br.ReadInt16().ToString();
                            keys["Players"] = br.ReadByte().ToString();
                            keys["MaxPlayers"] = br.ReadByte().ToString();
                            keys["Bots"] = br.ReadByte().ToString();
                            char c = br.ReadChar();
                            keys["ServerType"] = c == 'd' ? "Dedicated" : c == 'l' ? "Listen" : "SourceTV";
                            c = br.ReadChar();
                            keys["Environment"] = c == 'w' ? "Windows" : c == 'l' ? "Linux" : "Mac";
                            keys["Visibility"] = br.ReadBoolean() ? "Private" : "Public";
                            keys["VAC"] = br.ReadBoolean() ? "Secured" : "Unsecured";

                            if (int.Parse(keys["ID"]) == 2400) // The Ship
                            {
                                keys["Mode"] = br.ReadByte().ToString();
                                keys["Witnesses"] = br.ReadByte().ToString();
                                keys["Duration"] = br.ReadByte().ToString();
                            }

                            keys["Version"] = ReadString(br);

                            byte edf = br.ReadByte();
                            if ((edf & 0x80) == 1) { keys["Port"] = br.ReadInt16().ToString(); }
                            if ((edf & 0x10) == 1) { keys["SteamID"] = br.ReadUInt64().ToString(); }
                            if ((edf & 0x40) == 1)
                            {
                                keys["SpectatorPort"] = br.ReadInt16().ToString();
                                keys["SpectatorName"] = ReadString(br);
                            }
                            if ((edf & 0x20) == 1)
                            {
                                keys["Keywords"] = ReadString(br);

                                if (keys["Game"] == "Mordhau")
                                {
                                    string[] tags = keys["Keywords"].Split(',');

                                    foreach (string tag in tags)
                                    {
                                        if (tag[0] == 'B' && tag[1] == ':' && int.TryParse(tag.Replace("B:", string.Empty), out int players))
                                        {
                                            keys["Players"] = players.ToString();
                                            break;
                                        }
                                    }
                                }
                            }
                            if ((edf & 0x01) == 1) { keys["GameID"] = br.ReadUInt64().ToString(); }
                        }
                        else if (header == GOLDSOURCE_RESPONSE)
                        {
                            keys["Header"] = header.ToString();
                            keys["Address"] = ReadString(br);
                            keys["Name"] = ReadString(br);
                            keys["Map"] = ReadString(br);
                            keys["Folder"] = ReadString(br);
                            keys["Game"] = ReadString(br);
                            keys["Address"] = ReadString(br);
                            keys["Players"] = br.ReadByte().ToString();
                            keys["MaxPlayers"] = br.ReadByte().ToString();
                            keys["Protocol"] = br.ReadByte().ToString();
                            char c = char.ToLower(br.ReadChar());
                            keys["ServerType"] = c == 'd' ? "Dedicated" : c == 'l' ? "Listen" : "HLTV";
                            c = br.ReadChar();
                            keys["Environment"] = c == 'w' ? "Windows" : c == 'l' ? "Linux" : "Mac";
                            keys["Visibility"] = br.ReadBoolean() ? "Private" : "Public";
                            keys["Mod"] = br.ReadBoolean().ToString();

                            if (bool.Parse(keys["Mod"]))
                            {
                                keys["Link"] = ReadString(br);
                                keys["DownloadLink"] = ReadString(br);
                                br.ReadByte();
                                keys["Version"] = br.ReadInt64().ToString();
                                keys["Size"] = br.ReadInt64().ToString();
                                keys["Type"] = br.ReadByte().ToString();
                                keys["DLL"] = br.ReadByte().ToString();
                            }

                            keys["VAC"] = br.ReadBoolean() ? "Secured" : "Unsecured";
                            keys["Bots"] = br.ReadByte().ToString();
                        }
                    }

                    return keys.Count <= 0 ? null : keys;
                }
                catch
                {
                    return null;
                }
            });
        }

        /// <summary>Retrieves information about the players currently on the server.</summary>
        /// <returns>Returns (id, (name, score, timeConnected))</returns>
        public async Task<List<PlayerData>> GetPlayersData()
        {
            return await Task.Run(() =>
            {
                try
                {
                    byte[] requestData;
                    byte[] responseData;
                    using (UdpClientHandler udpHandler = new(_IPEndPoint))
                    {
                        // Send A2S_PLAYER request
                        requestData = [0xFF, 0xFF, 0xFF, 0xFF, .. A2S_INFO, .. new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }];

                        responseData = [.. udpHandler.GetResponse(requestData, requestData.Length, _timeout, _timeout).Skip(5)];

                        // Send A2S_PLAYER request with challenge
                        requestData = [0xFF, 0xFF, 0xFF, 0xFF, .. A2S_PLAYER, .. responseData];

                        // Receive response
                        responseData = [.. udpHandler.GetResponse(requestData, requestData.Length, _timeout, _timeout).Skip(4)];
                    }

                    // Store response's data
                    List<PlayerData> keys = [];

                    // Load response's data
                    using (BinaryReader br = new(new MemoryStream(responseData), Encoding.UTF8))
                    {
                        br.ReadByte(); // Header
                        int players = br.ReadByte();

                        for (int i = 0; i < players; i++)
                        {
                            br.ReadByte(); // index
                            string name = ReadString(br);
                            int score = br.ReadInt32();
                            TimeSpan timeConnected = TimeSpan.FromSeconds((int)br.ReadSingle());

                            keys.Add(new PlayerData(i, name, score, timeConnected));
                        }
                    }

                    return keys;
                }
                catch
                {
                    return null;
                }
            });
        }

        private static string ReadString(BinaryReader br)
        {
            byte[] bytes = [];

            // Get all bytes until 0x00
            do
            {
                bytes = [.. bytes, .. new[] { br.ReadByte() }];
            }
            while (bytes[^1] != 0x00);

            // Return bytes in UTF8 except the last byte because it is 0x00
            return Encoding.UTF8.GetString([.. bytes.Take(bytes.Length - 1)]);
        }

        public async Task<string> GetPlayersAndMaxPlayers()
        {
            try
            {
                Dictionary<string, string> kv = await GetInfo();
                return kv != null ? kv["Players"] + '/' + kv["MaxPlayers"] : null;
            } catch
            {
                return null;
            }
        }
    }
}
