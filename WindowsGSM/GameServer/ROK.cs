﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

/// <summary>
/// ROK server has a Server.exe which is good. But redirect standard input fail
/// </summary>

namespace WindowsGSM.GameServer
{
    class ROK(Functions.ServerConfig serverData) : Engine.Unity
    {
        private readonly Functions.ServerConfig _serverData = serverData;

        public string Error;
        public string Notice = string.Empty;

        public const string FullName = "Reign Of Kings Dedicated Server";
        public string StartPath = "ROK.exe";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 1;
        public dynamic QueryMethod = null;

        public string Port = "7350";
        public string QueryPort = "7350";
        public string Defaultmap = "CrownLand";
        public string Maxplayers = "30";
        public string Additional = string.Empty;

        public string AppId = "381690";

        public async void CreateServerCFG()
        {
            //Download ServerSettings.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Configuration", "ServerSettings.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{ServerName}}", _serverData.ServerName);
                configText = configText.Replace("{{Maxplayers}}", Maxplayers);
                configText = configText.Replace("{{ServerIP}}", _serverData.ServerIP);
                configText = configText.Replace("{{ServerPort}}", _serverData.ServerPort);
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

            string serverPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Server.exe");
            if (!File.Exists(serverPath))
            {
                Error = $"{Path.GetFileName(serverPath)} not found ({serverPath})";
                return null;
            }

            string param = $"-batchmode -nographics -silent-crashes {_serverData.ServerParam}";

            Process p = new() {
                StartInfo =
                {
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = serverPath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };
            p.Start();

            return p;
        }

        public static async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "/shutdown");
                Task.Delay(5000); //Wait 5 second for auto close
            });
        }

        public async Task<Process> Install()
        {
            Installer.SteamCMD steamCMD = new();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId, true);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            (Process p, string error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom);
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
            Installer.SteamCMD steamCMD = new();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            Installer.SteamCMD steamCMD = new();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
