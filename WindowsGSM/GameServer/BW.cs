﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.GameServer
{
    class BW
    {
        private readonly Functions.ServerConfig _serverData;

        public string Error;
        public string Notice;

        public const string FullName = "BlackWake Dedicated Server";
        public string StartPath = "BlackwakeServer.exe";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 3;
        public dynamic QueryMethod = new Query.A2S();

        public string Port = "25001";
        public string QueryPort = "25002";
        public string Defaultmap = "Island";
        public string Maxplayers = "54";
        public string Additional = string.Empty;

        public string AppId = "423410";

        public BW(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Server.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, _serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{serverName}}", _serverData.ServerName);
                configText = configText.Replace("{{serverIP}}", _serverData.ServerIP);
                configText = configText.Replace("{{serverPort}}", _serverData.ServerPort);
                configText = configText.Replace("{{steamPort}}", (int.Parse(_serverData.ServerPort) + 2014).ToString());
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string exePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(exePath))
            {
                Error = $"{Path.GetFileName(exePath)} 找不到 ({exePath})";
                return null;
            }

            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Server.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"{Path.GetFileName(configPath)} 找不到 ({configPath})";
            }

            string param = "-batchmode -nographics " + _serverData.ServerParam;

            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = exePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true,
            };
            p.Start();

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                p.Kill();
            });
        }

        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom);
            Error = error;
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            Error = $"無效路徑! 找不到 {Path.GetFileName(StartPath)}";
            return File.Exists(Path.Combine(path, StartPath));
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
