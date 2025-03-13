﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.GameServer
{
    class SW
    {
        private readonly Functions.ServerConfig _serverData;

        public string Error;
        public string Notice;

        public const string FullName = "Stormworks Dedicated Server";
        public string StartPath = "server.exe";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 3;
        public dynamic QueryMethod = null;

        public string Port = "25564";
        public string QueryPort = "25564";
        public string Defaultmap = "data/tiles/island12.xml";
        public string Maxplayers = "32";
        public string Additional = string.Empty;

        public string AppId = "1247090";

        public SW(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"AppData\Roaming\Stormworks", "server_config.xml");
            if (await Functions.Github.DownloadGameServerConfig(configPath, _serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{maxplayers}}", _serverData.ServerMaxPlayer);
                configText = configText.Replace("{{port}}", _serverData.ServerPort);
                configText = configText.Replace("{{ServerName}}", _serverData.ServerName);
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"AppData\Roaming\Stormworks", "server_config.xml");
            if (!File.Exists(configPath))
            {
                Notice = $"{Path.GetFileName(configPath)} 找不到 ({configPath})";
            }

            string param = _serverData.ServerParam;

            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath),
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true,  
            };
            //Change APPDATA directory
            p.StartInfo.EnvironmentVariables["USERPROFILE"] = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);
            p.StartInfo.EnvironmentVariables["APPDATA"] = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);
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
