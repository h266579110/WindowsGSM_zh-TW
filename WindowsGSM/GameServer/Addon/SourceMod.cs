using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;

namespace WindowsGSM.GameServer.Addon
{
    class SourceMod
    {
        public static async Task<bool> Install(string serverId, string modFolder)
        {
            string version = "1.10";
            string path = Functions.ServerPath.GetServersServerFiles(serverId, modFolder);

            try {
                string fileName = await App.httpClient.GetStringAsync($"https://sm.alliedmods.net/smdrop/{version}/sourcemod-latest-windows");
                string filePath = Path.Combine(path, fileName.Trim());
                Stream stream = await App.httpClient.GetStreamAsync($"https://sm.alliedmods.net/smdrop/{version}/{fileName}");
                using FileStream fileStream = File.Create(filePath);
                await stream.CopyToAsync(fileStream);
                //using WebClient webClient = new();
                //string fileName = await webClient.DownloadStringTaskAsync($"https://sm.alliedmods.net/smdrop/{version}/sourcemod-latest-windows");
                //await webClient.DownloadFileTaskAsync($"https://sm.alliedmods.net/smdrop/{version}/{fileName}", Path.Combine(path, fileName));
                await Task.Run(() => { try { ZipFile.ExtractToDirectory(filePath, path); } catch { } });
                await Task.Run(() => { try { File.Delete(filePath); } catch { } });
            } catch {
                return false;
            }

            return await Install_MetaMod_Source(serverId, modFolder);
        }

        private static async Task<bool> Install_MetaMod_Source(string serverId, string modFolder)
        {
            string version = "1.10";
            string path = Functions.ServerPath.GetServersServerFiles(serverId, modFolder);

            try {
                string fileName = await App.httpClient.GetStringAsync($"https://mms.alliedmods.net/mmsdrop/{version}/mmsource-latest-windows");
                string filePath = Path.Combine(path, fileName.Trim());
                Stream stream = await App.httpClient.GetStreamAsync($"https://mms.alliedmods.net/mmsdrop/{version}/{fileName}");
                using FileStream fileStream = File.Create(filePath);
                await stream.CopyToAsync(fileStream);
                //using WebClient webClient = new();
                //string fileName = await webClient.DownloadStringTaskAsync($"https://mms.alliedmods.net/mmsdrop/{version}/mmsource-latest-windows");
                //await webClient.DownloadFileTaskAsync($"https://mms.alliedmods.net/mmsdrop/{version}/{fileName}", Path.Combine(path, fileName));
                await Task.Run(() => { try { ZipFile.ExtractToDirectory(filePath, path); } catch { } });
                await Task.Run(() => { try { File.Delete(filePath); } catch { } });

                return true;
            } catch {
                return false;
            }
        }
    }
}
