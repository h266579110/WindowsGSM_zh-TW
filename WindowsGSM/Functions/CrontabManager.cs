using NCrontab;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static WindowsGSM.MainWindow;

namespace WindowsGSM.Functions
{
    public enum CrontabType
    {
        None = 0,
        Restart = 1,
        Exec = 2,
        ServerConsoleCommand = 3
    }

    public struct CrontabEntry(CrontabSchedule expression, CrontabType type, string command = "", string payload = "") {
        public CrontabSchedule Expression = expression;
        public CrontabType Type = type;
        public string Command = command;
        public string Payload = payload;
    }
    /** 
     * Manages the crontab sceduling and execution. As this method calls some Window Functions that access the GUI, you can not call this module from any other than the main thread (async is fine though).
     * It will immediatly kill that thread without exception or trace
     * To convert this into a seperatly running thread, one would need to remove all Window.Log(also check the other Window calls, they could include a log) functions and the Window.UpdateCrontabTime
     */
    public class CrontabManager
    {
        private MainWindow Window { get; }
        private ServerTable Server { get; set; }
        private Process Process { get; set; }

        private const string ConfigFolder = "Crontab";
        private List<CrontabEntry> crontabSchedules;
        private bool runLoop = true;
        private readonly List<Task> runningBackgroundTasks = [];

        public CrontabManager(MainWindow window, ServerTable server, Process process)
        {
            Window = window;
            Server = server;
            Process = process;

            CreateConfigDirectory();
            LoadCrontabConfig();
        }

        private void CreateConfigDirectory()
        {
            string configFolder = ServerPath.GetServersConfigs(Server.ID, "Crontab");
            if (!Directory.Exists(configFolder))
            {
                DirectoryInfo directory = Directory.CreateDirectory(configFolder);
                //set readOnly
                System.Security.Principal.SecurityIdentifier admin = new(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
                DirectorySecurity directorySecurity = directory.GetAccessControl();

                FileSystemAccessRule administratorRule = new(admin, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);

                directorySecurity.SetAccessRuleProtection(true, false);
                directorySecurity.AddAccessRule(administratorRule);

                directory.SetAccessControl(directorySecurity);
            }
        }

        public void LoadCrontabConfig()
        {
            string configFolder = ServerPath.GetServersConfigs(Server.ID, ConfigFolder);

            crontabSchedules = [];
            //add gui entry
            crontabSchedules.AddEntry(GetServerMetadata(int.Parse(Server.ID)).CrontabFormat, CrontabType.Restart);

            string[] files = Directory.GetFiles(configFolder, "*.csv", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);

                foreach (string line in lines)
                {
                    if (line[0] == '/' && line[1] == '/')
                    {
                        continue;
                    }
                    string[] tokens = line.Split(';');
                    if (tokens.Length == 2 && tokens[1].ToLower().Trim() == "restart")
                    {
                        crontabSchedules.AddEntry(tokens[0], CrontabType.Restart);
                        continue;
                    }

                    if (tokens.Length >= 3)
                    {
                        if (!Enum.TryParse(tokens[1].Trim(), true, out CrontabType type)) continue;

                        switch (type)
                        {
                            case CrontabType.None:
                                continue;
                            case CrontabType.Restart:
                                crontabSchedules.AddEntry(tokens[0], CrontabType.Restart);
                                continue;
                            case CrontabType.ServerConsoleCommand:
                                //we want to gather everything after the xth ;, as the commands could include some themself
                                string payload = line[(line.GetNthIndex(';', 2) + 1)..];
                                crontabSchedules.AddEntry(tokens[0], type, payload);
                                continue;
                            case CrontabType.Exec:
                                string execPayload = string.Empty;
                                if (tokens.Length >= 4)
                                {
                                    //we want to gather everything after the xth ;, as the commands could include some themself
                                    execPayload = line[(line.GetNthIndex(';', 3) + 1)..];
                                }
                                crontabSchedules.AddEntry(tokens[0], type, tokens[2], execPayload);
                                Console.WriteLine($" add exec entry with command {tokens[2]} and arguments {execPayload}");
                                continue;
                        }
                        continue;
                    }
                }
            }
            Console.WriteLine($"added a total of {crontabSchedules.Count} scedules for Server {Server.ID}");
        }

        public async Task MainLoop()
        {
            int serverId = int.Parse(Server.ID);
            runLoop = true;
            while (Process != null && !Process.HasExited && runLoop)
            {
                //If not enable return
                if (!GetServerMetadata(serverId).RestartCrontab || crontabSchedules == null)
                {
                    await Task.Delay(1000);
                    continue;
                }

                //Try get next DataTime restart
                //CrontabSchedule guiCrontab = CrontabSchedule.TryParse(Window.GetServerMetadata(serverId).CrontabFormat);
                //DateTime? crontabTime = guiCrontab?.GetNextOccurrence(DateTime.Now);
                List<(int index, DateTime? nextOccurrence)> nextOccurrences = [];
                for (int i = 0; i < crontabSchedules.Count; i++)
                {
                    nextOccurrences.Add((i, crontabSchedules[i].Expression?.GetNextOccurrence(DateTime.Now)));
                }

                //Delay 1 second for later compare
                await Task.Delay(1000);
                //execxute in a async task that runs simply in the background, as we don't want to miss any other scedules. could cause thread buildup, if something stupid is executed.

                foreach ((int index, DateTime? nextOccurrence) next in nextOccurrences)
                {
                    //Return if crontab expression is invalid 
                    if (next.nextOccurrence == null) continue;

                    //If now >= crontab time
                    if (DateTime.Compare(DateTime.Now, next.nextOccurrence ?? DateTime.Now) >= 0)
                    {
                        await ExecuteScedulesAsync(next);
                    }
                }
            }
            foreach (Task task in runningBackgroundTasks)
            {
                //explicitly kill all running background tasks
                task.Dispose();
            }
        }

        private async Task ExecuteScedulesAsync((int index, DateTime? nextOccurrence) next)
        {
            //Update the next crontab
            if (Process == null || Process.HasExited)
            {
                return;
            }

            //add switch for type
            CrontabEntry entry = crontabSchedules[next.index];
#if DEBUG
            Console.WriteLine($"執行排程: {entry.Expression}, {entry.Command}, {entry.Payload}");
#endif 
            switch (entry.Type)
            {
                case CrontabType.None:
                    return;
                case CrontabType.Restart:
                    await RestartServer();
                    Window.UpdateCrontabTime(Server.ID, entry.Expression?.GetNextOccurrence(DateTime.Now).ToString("yyyy/MM/dd/ddd HH:mm:ss"));
                    runLoop = false; //thread will be killed soon, so stop this crontab instance
                    return;
                case CrontabType.Exec:
                    Window.Log(Server.ID, $"執行排程: {entry.Command}");
                    runningBackgroundTasks.Add(ExecuteWindowsCommand(entry.Command, entry.Payload));
                    return;
                case CrontabType.ServerConsoleCommand:
                    Window.Log(Server.ID, $"執行排程: {entry.Command}");
                    ExecuteServerConsoleCommand(entry.Command);
                    return;
            }
        }

        /**
         * Runs the given Program in the background, piping the output to a file in the log-folder
         * The User/Programm is responsible to not run continous, the task will not stop by itself besides the server is restarting
         */
        private Task ExecuteWindowsCommand(string command, string arguments = "")
        {
            return Task.Run(() =>
            {
                Process p = new() {
                    StartInfo =
                    {
                        FileName = command,
                        Arguments = arguments,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = Encoding.UTF8,
                    },
                    EnableRaisingEvents = true
                };

                StringBuilder sb = new();
                p.Start();
                while (!p.StandardOutput.EndOfStream)
                {
                    sb.AppendLine(p.StandardOutput.ReadLine());
                    // do something with line
                }
                string file = ServerPath.GetLogs($"Server_{Server.ID}_{command}_execLog.log");
                File.AppendAllText(file, sb.ToString());
#if DEBUG
                Console.WriteLine($"Executed Exec, consoleData: {sb}");
#endif
            });
        }

        /**
         * Sends the given command to the ServerConsole of the current server
         */
        private void ExecuteServerConsoleCommand(string command)
        {
            Functions.ServerConsole.SetMainWindow(Process.MainWindowHandle);
            Functions.ServerConsole.SendWaitToMainWindow(command + "{ENTER}");
        }

        private async Task RestartServer()
        {
            int serverId = int.Parse(Server.ID);

            //Restart the server
            if (GetServerMetadata(Server.ID).ServerStatus == ServerStatus.Started)
            {
                //Begin Restart
                _serverMetadata[int.Parse(Server.ID)].ServerStatus = ServerStatus.Restarting;
                Window.Log(Server.ID, "操作: 重啟");
                Window.SetServerStatus(Server, "重啟中");

                await Window.Server_BeginStop(Server, Process);

                if (GetServerMetadata(Server.ID).UpdateOnStart)
                {
                    await Window.GameServer_Update(Server, " | 啟動時更新");
                }

                dynamic gameServer = await Window.Server_BeginStart(Server);
                if (gameServer == null) { return; }

                _serverMetadata[int.Parse(Server.ID)].ServerStatus = ServerStatus.Started;
                Window.Log(Server.ID, "伺服器: 已重啟 | 例行性重啟");
                if (!string.IsNullOrWhiteSpace(gameServer.Notice))
                {
                    Window.Log(Server.ID, "[注意] " + gameServer.Notice);
                }
                Window.SetServerStatus(Server, "已啟動", ServerCache.GetPID(Server.ID).ToString());

                if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).RestartCrontabAlert)
                {
                    DiscordWebhook webhook = new(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, Window.g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                    await webhook.Send(Server.ID, Server.Game, "已重啟 | 例行性重啟", Server.Name, await GetPublicIP(), Server.Port);
                    Window._latestWebhookSend = ServerStatus.Restarted;
                }
            }
        }
    }

    public static class CrontabExtensions
    {
        public static int GetNthIndex(this string s, char t, int n)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == t)
                {
                    count++;
                    if (count == n)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
        public static bool AddEntry(this List<CrontabEntry> entries, string expression, CrontabType type, string command = "", string payload = "")
        {
            CrontabSchedule scedule = CrontabSchedule.TryParse(expression);

            if (scedule != null)
            {
                entries.Add(new CrontabEntry(scedule, type, command, payload));
                return true;
            }
            return false;
        }
    }
}
