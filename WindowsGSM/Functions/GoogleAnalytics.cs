﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;



namespace WindowsGSM.Functions
{

    class GoogleAnalytics
    {
        /// <summary>
        /// https://developers.google.com/analytics/devguides/collection/protocol/v1/devguide
        /// </summary>

        private static readonly string _trackingId = "UA-131754595-13";
        private string _clientId;

        public async void SendWindowsGSMVersion()
        {
            string version = MainWindow.WGSM_VERSION;
            SendHit("WindowsGSMVersion", version, version);
        }

        public async void SendWindowsOS()
        {
            await Task.Run(() =>
            {
                // https://stackoverflow.com/questions/2819934/detect-windows-version-in-net
                string osBit = string.Empty;
                using (ManagementObjectSearcher searcher = new("SELECT OSArchitecture FROM Win32_OperatingSystem"))
                {
                    ManagementObjectCollection information = searcher.Get();
                    if (information != null)
                    {
                        foreach (ManagementObject obj in information.Cast<ManagementObject>())
                        {
                            osBit = obj["OSArchitecture"].ToString();
                        }
                    }
                }

                string osName = new Microsoft.VisualBasic.Devices.ComputerInfo().OSFullName;
                osBit = new string([.. osBit.Where(char.IsDigit)]);
                SendHit("OSVersion", osName, $"{osName} - {osBit}-bit");
            });
        }

        public async void SendProcessorName()
        {
            await Task.Run(() =>
            {
                string cpuName = string.Empty;
                using (ManagementObjectSearcher searcher = new("SELECT Name FROM Win32_Processor"))
                {
                    ManagementObjectCollection information = searcher.Get();
                    if (information != null)
                    {
                        foreach (ManagementObject obj in information.Cast<ManagementObject>())
                        {
                            cpuName = obj["Name"].ToString();
                        }
                    }
                }

                int coreCount = 0;
                foreach (ManagementBaseObject item in new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get())
                {
                    coreCount += int.Parse(item["NumberOfCores"].ToString());
                }
                
                SendHit("CPU", cpuName, $"{cpuName} - Cores: {coreCount}");
            });
        }

        public async void SendRAM()
        {
            await Task.Run(() =>
            {
                int count = 0;
                double total = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;

                while (total > 1024.0)
                {
                    total /= 1024.0;
                    count++;
                }

                string memory = Math.Ceiling(total) + (count == 1 ? "KB" : count == 2 ? "MB" : count == 3 ? "GB" : "TB");
                SendHit("RAM", memory, memory);
            });
        }

        public async void SendDisk()
        {
            await Task.Run(() =>
            {
                int diskCount = 0;
                double total = 0;

                foreach (DriveInfo info in DriveInfo.GetDrives())
                {
                    if (info.IsReady)
                    {
                        total += info.TotalSize / 1024.0;
                        diskCount++;
                    }
                }

                int count = 0;
                while (total > 1024.0)
                {
                    total /= 1024.0;
                    count++;
                }

                string disk = Math.Ceiling(total) + (count == 0 ? "KB" : count == 1 ? "MB" : count == 2 ? "GB" : count == 3 ? "TB" : "PB");
                SendHit("DISK", disk, $"{disk} - Count: {diskCount}");
            });
        }

        public async void SendGameServerInstall(string serverId, string serverGame)
        {
            await Task.Run(() =>
            {
                SendHit("Install", serverGame, $"{serverGame} #{serverId}");
            });
        }

        public async void SendGameServerStart(string serverId, string serverGame)
        {
            await Task.Run(() =>
            {
                SendHit("Start", serverGame, $"{serverGame} #{serverId}");
            });
        }

        public async void SendGameServerHeartBeat(string serverGame, string serverName)
        {
            await Task.Run(() =>
            {
                SendHit("HeartBeat", serverGame, serverName);
            });
        }

        private async void SendHit(string category, string action, string label, string value = null)
        {
            _clientId = string.IsNullOrEmpty(_clientId) ? GetClientID() : _clientId;
            if (string.IsNullOrEmpty(_clientId)) { return; }

            string post = $"v=1&t=event&tid={_trackingId}&cid={_clientId}";
            post += string.IsNullOrWhiteSpace(category) ? string.Empty : $"&ec={Uri.EscapeDataString(category)}";
            post += string.IsNullOrWhiteSpace(action) ? string.Empty : $"&ea={Uri.EscapeDataString(action)}";
            post += string.IsNullOrWhiteSpace(label) ? string.Empty : $"&el={Uri.EscapeDataString(label)}";
            post += string.IsNullOrWhiteSpace(value) ? string.Empty : $"&ev={Uri.EscapeDataString(value)}";

            try {
                HttpResponseMessage request = await App.httpClient.PostAsync("https://www.google-analytics.com/collect", new StringContent(post));

                if (request.StatusCode != HttpStatusCode.OK) {
                    Debug.WriteLine((int)request.StatusCode + "Google Analytics tracking did not return OK 200");
                }

                //HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.google-analytics.com/collect");
                //request.Method = "POST";
                //request.ContentType = "application/x-www-form-urlencoded";
                //request.ContentLength = Encoding.UTF8.GetByteCount(post);

                //// write the request body to the request
                //using (StreamWriter writer = new(await request.GetRequestStreamAsync()))
                //{
                //    writer.Write(post);
                //}

                //using HttpWebResponse webResponse = (HttpWebResponse)await request.GetResponseAsync();
                //if (webResponse.StatusCode != HttpStatusCode.OK) {
                //    Debug.WriteLine((int)webResponse.StatusCode + "Google Analytics tracking did not return OK 200");
                //}
            } catch (Exception ex) {
                Debug.WriteLine(ex);
            }
        }

        private static string GetClientID()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);
            if (key == null) { return null; }

            //Get Client ID
            string clientId = (key.GetValue("ClientID") == null) ? string.Empty : key.GetValue("ClientID").ToString();

            //If Client ID is invalid, set new client id
            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = Guid.NewGuid().ToString();
                key.SetValue("ClientID", clientId);
            }

            key.Close();

            return clientId;
        }
    }
}
