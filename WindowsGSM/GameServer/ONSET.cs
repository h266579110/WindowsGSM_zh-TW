﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.GameServer
{
    class ONSET(Functions.ServerConfig serverData) : Engine.UnrealEngine
    {
        private readonly Functions.ServerConfig _serverData = serverData;

        public string Error;
        public string Notice = string.Empty;

        public const string FullName = "Onset Dedicated Server";
        public string StartPath = "OnsetServer.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 3;
        public dynamic QueryMethod = null;

        public string Port = "7777";
        public string QueryPort = "7776";
        public string Defaultmap = string.Empty;
        public string Maxplayers = "100";
        public string Additional = string.Empty;

        public string AppId = "1204170";

        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server_config.json");
            if (await Functions.Github.DownloadGameServerConfig(configPath, _serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{servername}}", _serverData.ServerName);
                configText = configText.Replace("{{ipaddress}}", _serverData.ServerIP);
                configText = configText.Replace("{{port}}", _serverData.ServerPort);
                configText = configText.Replace("{{maxplayers}}", Maxplayers);
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

            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server_config.json");
            if (!File.Exists(configPath))
            {
                Notice = $"{Path.GetFileName(configPath)} 找不到 ({configPath})";
            }

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                        FileName = exePath,
                        Arguments = _serverData.ServerParam,
                        WindowStyle = ProcessWindowStyle.Minimized,
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
                        FileName = exePath,
                        Arguments = _serverData.ServerParam,
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
