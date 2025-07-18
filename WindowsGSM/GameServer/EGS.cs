﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace WindowsGSM.GameServer
{
    class EGS(Functions.ServerConfig serverData) {
        private readonly Functions.ServerConfig _serverData = serverData;

        public string Error;
        public string Notice = string.Empty;

        public const string FullName = "Empyrion - Galactic Survival Dedicated Server";
        public string StartPath = "EmpyrionLauncher.exe";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 5;
        public dynamic QueryMethod = null;

        public string Port = "30000";
        public string QueryPort = "30001";
        public string Defaultmap = "DediGame";
        public string Maxplayers = "8";
        public string Additional = "-dedicated dedicated.yaml";

        public string AppId = "530870";

        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "dedicated.yaml");
            if (await Functions.Github.DownloadGameServerConfig(configPath, _serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{Srv_Port}}", _serverData.ServerPort);
                configText = configText.Replace("{{Srv_Name}}", _serverData.ServerName);
                configText = configText.Replace("{{Srv_Password}}", Functions.ServerConfig.GetRCONPassword());
                configText = configText.Replace("{{Srv_MaxPlayers}}", _serverData.ServerMaxPlayer);
                configText = configText.Replace("{{Tel_Port}}", (int.Parse(_serverData.ServerPort) + 4).ToString());
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

            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "dedicated.yaml");
            if (!File.Exists(configPath))
            {
                Notice = $"{Path.GetFileName(configPath)} 找不到 ({configPath})";
            }

            Process p = new() {
                StartInfo =
                {
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = exePath,
                    Arguments = "-startDedi " + _serverData.ServerParam,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };
            p.Start();
            
            // Search UnityCrashHandler64.exe and return its commandline and get the dedicated process
            string crashHandler = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "DedicatedServer", "UnityCrashHandler64.exe");
            await Task.Delay(3000);
            for (int i = 0; i < 5; i++)
            {
                string commandLine = await Functions.ProcessManagement.GetCommandLineByApproximatePath(crashHandler);
                if (commandLine != null)
                {
                    try
                    {
                        Regex regex = new(@" --attach (\d{1,})"); // Match " --attach 7144"
                        string dedicatedProcessId = regex.Match(commandLine).Groups[1].Value; // Get first group -> "7144"
                        Process dedicatedProcess = await Task.Run(() => Process.GetProcessById(int.Parse(dedicatedProcessId)));
                        dedicatedProcess.StartInfo.CreateNoWindow = true; // Just set as metadata
                        return dedicatedProcess;
                    }
                    catch
                    {
                        Error = $"Fail to find {Path.GetFileName(exePath)}";
                        return null;
                    }
                }

                await Task.Delay(5000);
            }

            Error = "Fail to find UnityCrashHandler64.exe";
            return null;
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
