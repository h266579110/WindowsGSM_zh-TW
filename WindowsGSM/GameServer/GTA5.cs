using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System;
using Microsoft.CodeAnalysis.Recommendations;

namespace WindowsGSM.GameServer
{
    class GTA5(Functions.ServerConfig serverData) {
        private readonly Functions.ServerConfig _serverData = serverData;

        public string Error;
        public string Notice = string.Empty;

        public const string FullName = "Grand Theft Auto V Dedicated Server (FiveM)";
        public string StartPath = @"server\FXServer.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public dynamic QueryMethod = new Query.FIVEM();

        public string Port = "30120";
        public string QueryPort = "30120";
        public string Defaultmap = "fivem-map-skater";
        public string Maxplayers = "32";
        public string Additional = "+exec server.cfg";

        public async void CreateServerCFG()
        {
            //Download server.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"cfx-server-data-master\server.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{hostname}}", _serverData.ServerName);
                configText = configText.Replace("{{rcon_password}}", Functions.ServerConfig.GetRCONPassword());
                configText = configText.Replace("{{ip}}", Functions.ServerConfig.GetIPAddress());
                configText = configText.Replace("{{port}}", _serverData.ServerPort);
                configText = configText.Replace("{{maxplayers}}", Maxplayers);
                File.WriteAllText(configPath, configText);
            }

            //Download sample logo
            string logoPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"cfx-server-data-master\myLogo.png");
            await Functions.Github.DownloadGameServerConfig(logoPath, FullName);
        }

        public async Task<Process> Start()
        {
            string fxServerPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"server\FXServer.exe");
            if (!File.Exists(fxServerPath))
            {
                Error = $"FXServer.exe not found ({fxServerPath})";
                return null;
            }

            string citizenPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"server\citizen");
            if (!Directory.Exists(citizenPath))
            {
                Error = $"Directory citizen not found ({citizenPath})";
                return null;
            }

            string serverDataPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "cfx-server-data-master");
            if (!Directory.Exists(serverDataPath))
            {
                Error = $"Directory cfx-server-data-master not found ({serverDataPath})";
                return null;
            }

            string configPath = Path.Combine(serverDataPath, "server.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"server.cfg 找不到 ({configPath})";
            }

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = serverDataPath,
                        FileName = fxServerPath,
                        Arguments = $"+set citizen_dir \"{citizenPath}\" {_serverData.ServerParam}",
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
                        WorkingDirectory = serverDataPath,
                        FileName = fxServerPath,
                        Arguments = $"+set citizen_dir \"{citizenPath}\" {_serverData.ServerParam}",
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
                if (p.StartInfo.RedirectStandardInput)
                {
                    p.StandardInput.WriteLine("quit");
                }
                else
                {
                    Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "quit");
                }
            });
        }

        public async Task<Process> Install()
        {
            try {
                string html = await App.httpClient.GetStringAsync("https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/");
                //using WebClient webClient = new();
                //string html = await webClient.DownloadStringTaskAsync("https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/");
                Regex regex = new(@"[0-9]{4}-[ -~][^\s]{39}");
                MatchCollection matches = regex.Matches(html);

                if (matches.Count <= 0) {
                    return null;
                }

                //Match 1 is the latest recommended
                string recommended = regex.Match(html).ToString();

                //Download server.zip and extract then delete server.zip
                string serverPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server");
                Directory.CreateDirectory(serverPath);
                string zipPath = Path.Combine(serverPath, "server.zip");
                Stream stream = await App.httpClient.GetStreamAsync($"https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/{recommended}/server.zip");
                using FileStream fileStream = File.Create(zipPath);
                await stream.CopyToAsync(fileStream);
                //await webClient.DownloadFileTaskAsync($"https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/{recommended}/server.zip", zipPath);
                await Task.Run(() => {
                    try {
                        ZipFile.ExtractToDirectory(zipPath, serverPath);
                    } catch {
                        Error = "Path too long";
                    }
                });
                await Task.Run(() => File.Delete(zipPath));

                //Create FiveM-version.txt and write the downloaded version with hash
                File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "FiveM-version.txt"), recommended);

                //Download cfx-server-data-master and extract to folder cfx-server-data-master then delete cfx-server-data-master.zip
                zipPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "cfx-server-data-master.zip");
                stream = await App.httpClient.GetStreamAsync("https://github.com/citizenfx/cfx-server-data/archive/master.zip");
                using FileStream fileStream2 = File.Create(zipPath);
                await stream.CopyToAsync(fileStream2);
                //await webClient.DownloadFileTaskAsync("https://github.com/citizenfx/cfx-server-data/archive/master.zip", zipPath);
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, Functions.ServerPath.GetServersServerFiles(_serverData.ServerID)));
                await Task.Run(() => File.Delete(zipPath));

                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Process> Update()
        {
            try
            {
                //using WebClient webClient = new();
                string remoteBuild = await GetRemoteBuild();

                //Download server.zip and extract then delete server.zip
                string serverPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server");
                await Task.Run(() => {
                    try {
                        Directory.Delete(serverPath, true);
                    } catch {
                        //ignore
                    }
                });

                if (Directory.Exists(serverPath)) {
                    Error = $"Unable to delete server folder. Path: {serverPath}";
                    return null;
                }

                Directory.CreateDirectory(serverPath);
                string zipPath = Path.Combine(serverPath, "server.zip");
                Stream stream = await App.httpClient.GetStreamAsync($"https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/{remoteBuild}/server.zip");
                using FileStream fileStream = File.Create(zipPath);
                await stream.CopyToAsync(fileStream);
                //await webClient.DownloadFileTaskAsync($"https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/{remoteBuild}/server.zip", zipPath);
                await Task.Run(() => {
                    try {
                        ZipFile.ExtractToDirectory(zipPath, serverPath);
                    } catch {
                        Error = "Path too long";
                    }
                });
                await Task.Run(() => File.Delete(zipPath));

                //Create FiveM-version.txt and write the downloaded version with hash
                File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "FiveM-version.txt"), remoteBuild);

                return null;
            }
            catch
            {
                return null;
            }
        }

        public bool IsInstallValid()
        {
            string exeFile = @"server\FXServer.exe";
            string exePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, exeFile);

            return File.Exists(exePath);
        }

        public bool IsImportValid(string path)
        {
            string exeFile = @"server\FXServer.exe";
            string exePath = Path.Combine(path, exeFile);

            Error = $"無效路徑! 找不到 {exeFile}";
            return File.Exists(exePath);
        }

        public string GetLocalBuild()
        {
            string versionPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "FiveM-version.txt");
            Error = $"Fail to get local build";
            return File.Exists(versionPath) ? File.ReadAllText(versionPath) : string.Empty;
        }

        public async Task<string> GetRemoteBuild()
        {
            try
            {
                string html = await App.httpClient.GetStringAsync("https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/");
                //using WebClient webClient = new();
                //string html = await webClient.DownloadStringTaskAsync("https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/");
                Regex regex = new(@"[0-9]{4}-[ -~][^\s]{39}");
                MatchCollection matches = regex.Matches(html);

                return matches[0].Value;
            }
            catch
            {
                //ignore
            }

            Error = $"Fail to get remote build";
            return string.Empty;
        }
    }
}
