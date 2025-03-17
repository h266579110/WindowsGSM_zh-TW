﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.GameServer
{
    class TF(Functions.ServerConfig serverData) {
        private readonly Functions.ServerConfig _serverData = serverData;

        public string Error;
        public string Notice = string.Empty;

        public const string FullName = "The Forest Dedicated Server";
        public string StartPath = "TheForestDedicatedServer.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = null;

        public string Port = "27015";
        public string QueryPort = "27016";
        public string Defaultmap = "";
        public string Maxplayers = "4";
        public string Additional = "-configfilepath \"Server.cfg\"";

        public string AppId = "556450";

        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Server.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, _serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{serverIP}}", _serverData.ServerIP);
                configText = configText.Replace("{{serverSteamPort}}", (int.Parse(_serverData.ServerPort) - 18249).ToString());
                configText = configText.Replace("{{serverGamePort}}", _serverData.ServerPort);
                configText = configText.Replace("{{serverQueryPort}}", _serverData.ServerQueryPort);
                configText = configText.Replace("{{serverName}}", _serverData.ServerName);
                configText = configText.Replace("{{serverPlayers}}", _serverData.ServerMaxPlayer);
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Server.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"{Path.GetFileName(configPath)} 找不到 ({configPath})";
            }

            string param = $"-batchmode -dedicated {_serverData.ServerParam}";

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                        FileName = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath),
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                        FileName = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath),
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                Functions.ServerConsole serverConsole = new(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        public static async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                p.Kill();
            });
        }

        public async Task<Process> Install()
        {
            Installer.SteamCMD steamCMD = new();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId);
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
            string importPath = Path.Combine(path, StartPath);
            Error = $"無效路徑! 找不到 {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
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
