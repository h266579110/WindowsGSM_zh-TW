﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WindowsGSM.GameServer
{
    class SQ
    {
        private readonly Functions.ServerConfig _serverData;

        public string Error;
        public string Notice;

        public const string FullName = "Squad Dedicated Server";
        public string StartPath = @"SquadGame\Binaries\Win64\SquadGameServer.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new Query.A2S();

        public string Port = "7787";
        public string QueryPort = "27165";
        public string Defaultmap = "";
        public string Maxplayers = "80";
        public string Additional = "RANDOM=ALWAYS";

        public string AppId = "403240";

        public SQ(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"SquadGame\ServerConfig", "Server.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, _serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{ServerName}}", _serverData.ServerName);
                configText = configText.Replace("{{MaxPlayers}}", _serverData.ServerMaxPlayer);
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"SquadGame\ServerConfig", "Server.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"{Path.GetFileName(configPath)} 找不到 ({configPath})";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"-log");
            sb.Append(string.IsNullOrWhiteSpace(_serverData.ServerIP) ? string.Empty : $" MULTIHOME={_serverData.ServerIP}");
            sb.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" Port={_serverData.ServerPort}");
            sb.Append(string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" QueryPort={_serverData.ServerQueryPort}");
            sb.Append(string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" FIXEDMAXPLAYERS={_serverData.ServerMaxPlayer}");
            sb.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");
            string param = sb.ToString();

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
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p.StartInfo.CreateNoWindow)
                {
                    p.Kill();
                }
                else
                {
                    p.CloseMainWindow();
                }
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
            string importPath = Path.Combine(path, "SquadGameServer.exe");
            Error = $"無效路徑! 找不到 {Path.GetFileName(importPath)}";
            return File.Exists(importPath);
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
