using System;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net.Http;

namespace WindowsGSM.Functions
{
    class InstallAddons
    {
        public static bool? IsAMXModXAndMetaModPExists(Functions.ServerTable server)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game);
            if (gameServer is not GameServer.Engine.GoldSource)
            {
                // Game Type not supported
                return null;
            }

            string MMPath = Functions.ServerPath.GetServersServerFiles(server.ID, gameServer.Game, "addons\\metamod.dll");
            return Directory.Exists(MMPath);
        }

        public static async Task<bool> AMXModXAndMetaModP(Functions.ServerTable server)
        {
            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(server.Game);
                return await GameServer.Addon.AMXModX.Install(server.ID, modFolder: gameServer.Game);
            }
            catch
            {
                return false;
            }
        }

        public static bool? IsSourceModAndMetaModExists(Functions.ServerTable server)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game);
            if (gameServer is not GameServer.Engine.Source)
            {
                // Game Type not supported
                return null;
            }

            string SMPath = Functions.ServerPath.GetServersServerFiles(server.ID, gameServer.Game, "addons\\sourcemod");
            return Directory.Exists(SMPath);
        }

        public static async Task<bool> SourceModAndMetaMod(Functions.ServerTable server)
        {
            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(server.Game);
                return await GameServer.Addon.SourceMod.Install(server.ID, modFolder: gameServer.Game);
            }
            catch
            {
                return false;
            }
        }

        public static bool? IsDayZSALModServerExists(Functions.ServerTable server)
        {
            if (server.Game != GameServer.DAYZ.FullName)
            {
                // Game Type not supported
                return null;
            }

            string exePath = Functions.ServerPath.GetServersServerFiles(server.ID, "DZSALModServer.exe");
            return File.Exists(exePath);
        }

        public static async Task<bool> DayZSALModServer(Functions.ServerTable server)
        {
            try
            {
                string zipPath = Functions.ServerPath.GetServersServerFiles(server.ID, "dzsalmodserver.zip");
                Stream stream = await App.httpClient.GetStreamAsync("http://dayzsalauncher.com/releases/dzsalmodserver.zip");
                using FileStream fileStream = File.Create(zipPath);
                await stream.CopyToAsync(fileStream);
                //using WebClient webClient = new();
                //await webClient.DownloadFileTaskAsync("http://dayzsalauncher.com/releases/dzsalmodserver.zip", zipPath);
                await Task.Run(() => { try { ZipFile.ExtractToDirectory(zipPath, Functions.ServerPath.GetServersServerFiles(server.ID)); } catch { } });
                await Task.Run(() => { try { File.Delete(zipPath); } catch { } });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool? IsOxideModExists(Functions.ServerTable server)
        {
            if (server.Game != GameServer.RUST.FullName)
            {
                // Game Type not supported
                return null;
            }

            return File.Exists(Functions.ServerPath.GetServersServerFiles(server.ID, "RustDedicated_Data", "Managed", "Oxide.Core.dll"));
        }

        public static async Task<bool> OxideMod(Functions.ServerTable server)
        {
            try
            {
                string basePath = Functions.ServerPath.GetServersServerFiles(server.ID);
                string zipPath = Functions.ServerPath.GetServersServerFiles(server.ID, "Oxide.Rust.zip");
                Stream stream = await App.httpClient.GetStreamAsync("https://github.com/OxideMod/Oxide.Rust/releases/latest/download/Oxide.Rust.zip");
                using FileStream fileStream = File.Create(zipPath);
                await stream.CopyToAsync(fileStream);
                //using WebClient webClient = new();
                //await webClient.DownloadFileTaskAsync("https://github.com/OxideMod/Oxide.Rust/releases/latest/download/Oxide.Rust.zip", zipPath);

                bool success = await Task.Run(() =>
                {
                    try
                    {
                        using FileStream f = File.OpenRead(zipPath);
                        using ZipArchive a = new(f);
                        a.Entries.Where(o => o.Name == string.Empty && !Directory.Exists(Path.Combine(basePath, o.FullName))).ToList().ForEach(o => Directory.CreateDirectory(Path.Combine(basePath, o.FullName)));
                        a.Entries.Where(o => o.Name != string.Empty).ToList().ForEach(e => e.ExtractToFile(Path.Combine(basePath, e.FullName), true));
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });

                await Task.Run(() => { try { File.Delete(zipPath); } catch { } });
                return success;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string> GetOxideModLatestVersion()
        {
            try {
                HttpResponseMessage request = await App.httpClient.GetAsync("https://api.github.com/repos/OxideMod/Oxide.Rust/releases/latest");
                using StreamReader responseReader = new(request.Content.ReadAsStream());
                return JObject.Parse(responseReader.ReadToEnd())["tag_name"].ToString();
                //HttpWebRequest webRequest = WebRequest.Create("https://api.github.com/repos/OxideMod/Oxide.Rust/releases/latest") as HttpWebRequest;
                //webRequest.Method = "GET";
                //webRequest.Headers["User-Agent"] = "Anything";
                //webRequest.ServicePoint.Expect100Continue = false;
                //WebResponse response = await webRequest.GetResponseAsync();
                //using StreamReader responseReader = new(response.GetResponseStream());
                //return JObject.Parse(responseReader.ReadToEnd())["tag_name"].ToString();
            } catch
            {
                return null;
            }
        }
    }
}
