﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WindowsGSM.GameServer
{
    /// <summary>
    /// 
    /// Note:
    /// I have tested the input output thing.
    /// 
    /// RedirectStandardInput:  NO WORKING
    /// RedirectStandardOutput: NO WORKING
    /// RedirectStandardError:  NO WORKING
    /// SendKeys Input Method:  NO WORKING
    /// 
    /// Therefore, traditional method is used.
    /// 
    /// </summary>
    class DAYZ
    {
        private readonly Functions.ServerConfig _serverData;

        public string Error;
        public string Notice;

        public const string FullName = "DayZ Dedicated Server";
        public string StartPath = "DayZServer_x64.exe";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new Query.A2S();
        public bool loginAnonymous = false;

        public string Port = "2302";
        public string QueryPort = "2305";
        public string Defaultmap = "dayzOffline.chernarusplus";
        public string Maxplayers = "60";
        public string Additional = "-config=serverDZ.cfg -doLogs -adminLog -netLog -profiles=profile";

        public string AppId = "223350";

        public DAYZ(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        public async void CreateServerCFG()
        {
            //Download serverDZ.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "serverDZ.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                StringBuilder configText = new StringBuilder( File.ReadAllText(configPath));
                configText = configText.Replace("{{hostname}}", _serverData.ServerName);
                configText = configText.Replace("{{maxplayers}}", Maxplayers);
                configText.AppendLine("steamProtocolMaxDataSize = 4000; //should allow for more mods as this somehow affects how many parameters can be added via commandline "); 
                configText.AppendLine("enableCfgGameplayFile = 0;");
                configText.AppendLine("logFile = \"server_console.log\";");
                configText.AppendLine($"steamQueryPort = {QueryPort};            // defines Steam query port,"); 
                File.WriteAllText(configPath, configText.ToString());
            }
        }

        public async Task<Process> Start()
        {
            // Use DZSALModServer.exe if the exe exist, otherwise use original
            string dzsaPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "DZSALModServer.exe");
            if (File.Exists(dzsaPath))
            {
                StartPath = "DZSALModServer.exe";

                WindowsFirewall firewall = new WindowsFirewall(StartPath, dzsaPath);
                if (!await firewall.IsRuleExist())
                {
                    await firewall.AddRule();
                }
            }
            else
            {
                string serverPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
                if (!File.Exists(serverPath))
                {
                    Error = $"{StartPath} 找不到 ({serverPath})";
                    return null;
                }
            }

            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "serverDZ.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"{Path.GetFileName(configPath)} 找不到 ({configPath})";
            }

            string param = $" {_serverData.ServerParam}";
            param += string.IsNullOrEmpty(_serverData.ServerIP) ? string.Empty : $" -ip={_serverData.ServerIP}";
            param += string.IsNullOrEmpty(_serverData.ServerPort) ? string.Empty : $" -port={_serverData.ServerPort}";

            string modPath = Functions.ServerPath.GetServersConfigs(_serverData.ServerID, "DayZActivatedMods.cfg");
            if (File.Exists(modPath))
            {
                string modParam = string.Empty;
                foreach (string modName in File.ReadLines(modPath))
                {
                    modParam += $"{modName.Trim()};";
                }

                if (!string.IsNullOrWhiteSpace(modParam))
                {
                    param += $" \"-mod={modParam}\"";
                }
            }

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
                EnableRaisingEvents = true
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
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId, true, loginAnonymous);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
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
