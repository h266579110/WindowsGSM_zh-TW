﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.GameServer
{
    class CE(Functions.ServerConfig serverData) {
        private readonly Functions.ServerConfig _serverData = serverData;

        public string Error;
        public string Notice = string.Empty;

        public const string FullName = "Conan Exiles Dedicated Server";
        public string StartPath = @"ConanSandbox\Binaries\Win64\ConanSandboxServer-Win64-Shipping.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new Query.A2S();

        public string Port = "7777";
        public string QueryPort = "27015";
        public string Defaultmap = "game.db";
        public string Maxplayers = "40";
        public string Additional = string.Empty;

        public string AppId = "443030";

        public async void CreateServerCFG()
        {
            //Download Engine.ini
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"ConanSandbox\Saved\Config\WindowsServer\Engine.ini");
            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{ServerName}}", _serverData.ServerName);
                configText = configText.Replace("{{ServerPassword}}", Functions.ServerConfig.GetRCONPassword());
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} 找不到 ({shipExePath})";
                return null;
            }

            string param = string.IsNullOrWhiteSpace(_serverData.ServerIP) ? string.Empty : $" -MultiHome={_serverData.ServerIP}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -Port={_serverData.ServerPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" -QueryPort={_serverData.ServerQueryPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" -MaxPlayers={_serverData.ServerMaxPlayer}";
            param += $" {_serverData.ServerParam}" + (!AllowsEmbedConsole ? " -log" : string.Empty);

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        FileName = shipExePath,
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
                        FileName = shipExePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false,
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
                if (p.StartInfo.CreateNoWindow)
                {
                    p.CloseMainWindow();
                }
                else
                {
                    Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                    Functions.ServerConsole.SendWaitToMainWindow("^c");
                }
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
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "ConanSandboxServer.exe"));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, "ConanSandboxServer.exe");
            Error = $"無效路徑! 找不到 {Path.GetFileName(exePath)}";
            return File.Exists(exePath);
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
