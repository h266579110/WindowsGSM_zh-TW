using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.GameServer.Engine
{
    public class SteamCMDAgent(Functions.ServerConfig serverData) {

        // Standard variables
        public Functions.ServerConfig serverData = serverData;
        public string Error { get; set; }
        public string Notice { get; set; }

        public virtual bool LoginAnonymous { get; set; }
        public virtual string AppId { get; set; }
        public virtual string StartPath { get; set; }

        public async Task<Process> Install()
        {
            Installer.SteamCMD steamCMD = new();
            Process p = await steamCMD.Install(serverData.ServerID, string.Empty, AppId, true, LoginAnonymous);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            (Process p, string error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: LoginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

        public string GetLocalBuild()
        {
            Installer.SteamCMD steamCMD = new();
            return steamCMD.GetLocalBuild(serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            Installer.SteamCMD steamCMD = new();
            return await steamCMD.GetRemoteBuild(AppId);
        }

        public bool IsInstallValid()
        {
            string installPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            Error = $"Fail to find {installPath}";
            return File.Exists(installPath);
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"無效路徑! 找不到 {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }
    }
}
