using Discord;
using System;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using WindowsGSM.Functions;

namespace WindowsGSM.DiscordBot
{
    public class Actions
    {
        public static async Task<Embed> GetServerList(string userId)
        {
            EmbedBuilder embed = new() { Color = Color.Teal };
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;

                System.Collections.Generic.List<(string, string, string)> list = WindowsGSM.GetServerListByUserId(userId);

                string ids = string.Empty;
                string status = string.Empty;
                string servers = string.Empty;

                foreach ((string id, string state, string server) in list)
                {
                    ids += $"`{id}`\n";
                    status += $"`{state}`\n";
                    servers += $"`{server}`\n";
                }

                embed.AddField("ID", ids, inline: true);
                embed.AddField("狀態", status, inline: true);
                embed.AddField("伺服器名稱", servers, inline: true);
            });

            return embed.Build();
        }

        private static async Task<string> GetServerName(string serverId)
        {
            return await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                return WindowsGSM.GetServerName(serverId);
            });
        }

        public static async Task<string> GetServerPermissions(string userId)
        {
            System.Collections.Generic.List<string> serverIds = Configs.GetServerIdsByAdminId(userId);
            return serverIds.Contains("0")
                ? "你擁有所有權限.\n指令: `check`, `list`, `start`, `stop`, `restart`, `send`, `backup`, `update`, `stats`"
                : $"你在伺服器 (`{string.Join(",", [.. serverIds])}`) 擁有權限\n指令: `check`, `start`, `stop`, `restart`, `send`, `backup`, `update`, `stats`";

        }

        public static async Task StartServer(SocketInteraction interaction, string serverId)
        {
            string serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                string message = string.Empty;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    MainWindow.ServerStatus serverStatus = MainWindow.GetServerStatus(serverId);
                    switch (serverStatus) {
                        case MainWindow.ServerStatus.Stopped:
                            bool started = await WindowsGSM.StartServerById(serverId, interaction.User.Id.ToString(), interaction.User.Username);
                            message = $"伺服器 {serverName}(ID: {serverId}) {(started ? "已啟動" : "啟動失敗")}";
                            break;
                        case MainWindow.ServerStatus.Started:
                            message = $"伺服器 {serverName}(ID: {serverId}) 已經啟動";
                            break;
                        default:
                            message = $"伺服器 {serverName}(ID: {serverId}) 目前狀態為 {serverStatus}，無法啟動";
                            break;
                    }

                    await SendServerEmbed(interaction, message, Color.Green, serverId,
                        MainWindow.GetServerStatus(serverId).ToString(), WindowsGSM.GetServerName(serverId));
                }
                else
                {
                    await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 不存在");
                }
            });
        }

        public static async Task StopServer(SocketInteraction interaction, string serverId)
        {
            string serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                string message = string.Empty;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    MainWindow.ServerStatus serverStatus = MainWindow.GetServerStatus(serverId);
                    switch (serverStatus) {
                        case MainWindow.ServerStatus.Started:
                        case MainWindow.ServerStatus.Starting:
                            bool started = await WindowsGSM.StopServerById(serverId, interaction.User.Id.ToString(), interaction.User.Username);
                            message = $"伺服器 {serverName}(ID: {serverId}) {(started ? "已停止" : "停止失敗")}";
                            break;
                        case MainWindow.ServerStatus.Stopped:
                            message = $"伺服器 {serverName}(ID: {serverId}) 已經停止";
                            break;
                        default:
                            message = $"伺服器 {serverName}(ID: {serverId}) 目前狀態為 {serverStatus}，無法停止";
                            break;
                    }

                    await SendServerEmbed(interaction, message, Color.Orange, serverId,
                        MainWindow.GetServerStatus(serverId).ToString(), WindowsGSM.GetServerName(serverId));
                }
                else
                {
                    await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 不存在");
                }
            });
        }

        public static async Task StopAllServer(SocketInteraction interaction)
        {
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                System.Collections.Generic.List<(string, string, string)> serverList = WindowsGSM.GetServerList();
                foreach ((string, string, string) server in serverList)
                {
                    if (WindowsGSM.IsServerExist(server.Item1))
                    {
                        MainWindow.ServerStatus serverStatus = MainWindow.GetServerStatus(server.Item1);
                        if (serverStatus == MainWindow.ServerStatus.Started || serverStatus == MainWindow.ServerStatus.Starting)
                        {
                            bool started = await WindowsGSM.StopServerById(server.Item1, interaction.User.Id.ToString(), interaction.User.Username);
                            await interaction.FollowupAsync($"伺服器 (ID: {server.Item1}) {(started ? "已停止" : "停止失敗")}.");
                        }
                        else if (serverStatus == MainWindow.ServerStatus.Stopped)
                        {
                            await interaction.FollowupAsync($"伺服器 (ID: {server.Item1}) 已停止");
                        }
                        else
                        {
                            await interaction.FollowupAsync($"伺服器 (ID: {server.Item1}) 目前狀態為 {serverStatus}，無法停止");
                        }
                    }
                }
            });
        }

        public static async Task RestartServer(SocketInteraction interaction, string serverId)
        {
            string serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                string message = string.Empty;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    MainWindow.ServerStatus serverStatus = MainWindow.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Started || serverStatus == MainWindow.ServerStatus.Starting)
                    {
                        bool started = await WindowsGSM.RestartServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        message = $"伺服器 {serverName}(ID: {serverId}) {(started ? "已重啟" : "重啟失敗")}.";
                    }
                    else
                    {
                        message = $"伺服器 {serverName}(ID: {serverId}) 目前狀態為 {serverStatus}，無法重啟";
                    }

                    await SendServerEmbed(interaction, message, Color.Blue, serverId,
                        MainWindow.GetServerStatus(serverId).ToString(), WindowsGSM.GetServerName(serverId));
                }
                else
                {
                    await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 不存在");
                }
            });
        }

        public static async Task SendServerCommand(SocketInteraction interaction, string serverId, string command, bool withResponce = false)
        {
            string serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    MainWindow.ServerStatus serverStatus = MainWindow.GetServerStatus(serverId);
                    if (IsStarted(serverStatus))
                    {
                        string sent = await WindowsGSM.SendCommandById(serverId, command,
                            interaction.User.Id.ToString(), interaction.User.Username);
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) {(string.IsNullOrWhiteSpace(sent) ? "指令已送出" : "發送指令失敗")}. | `{command}`");

                        if( withResponce && string.IsNullOrWhiteSpace(sent))
                        {
                            await interaction.FollowupAsync($"LastLog:"); //read last log (2k is the limit for dc messages
                            const int signsToSend = 1800;
                            for (int i = 0; i < sent.Length; i += signsToSend)
                            {
                                int len = i + signsToSend < sent.Length ? signsToSend : sent.Length - i;
                                await interaction.FollowupAsync($"```\n{sent.Substring(i, len)}\n```"); //read last log (2k is the limit for dc messages
                            }
                        }

                    }
                    else
                    {
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) 目前狀態為 {serverStatus}，無法發送指令");
                    }
                }
                else
                {
                    await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 不存在");
                }
            });
        }


        public static async Task BackupServer(SocketInteraction interaction, string serverId)
        {
            string serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    MainWindow.ServerStatus serverStatus = MainWindow.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Stopped)
                    {
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) 開始備份 - 這可能需要一些時間");
                        bool backuped = await WindowsGSM.BackupServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) {(backuped ? "備份完成" : "備份失敗")}.");
                    }
                    else if (serverStatus == MainWindow.ServerStatus.Backuping)
                    {
                        await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 正在備份");
                    }
                    else
                    {
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) 目前狀態為 {serverStatus}，無法備份");
                    }
                }
                else
                {
                    await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 不存在");
                }
            });
        }

        public static async Task UpdateServer(SocketInteraction interaction, string serverId)
        {
            string serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    MainWindow.ServerStatus serverStatus = MainWindow.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Stopped)
                    {
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) 開始更新 - 這可能需要一些時間");
                        bool updated = await WindowsGSM.UpdateServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) {(updated ? "已更新" : "更新失敗")}.");
                    }
                    else if (serverStatus == MainWindow.ServerStatus.Updating)
                    {
                        await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 正在更新");
                    }
                    else
                    {
                        await interaction.FollowupAsync(
                            $"伺服器 {serverName}(ID: {serverId}) 目前狀態為 {serverStatus}，無法更新");
                    }
                }
                else
                {
                    await interaction.FollowupAsync($"伺服器 {serverName}(ID: {serverId}) 不存在");
                }
            });
        }

        public static async Task GetServerStats(SocketInteraction interaction)
        {
            SystemMetrics system = new();
            await Task.Run(() => system.GetCPUStaticInfo());
            await Task.Run(() => system.GetRAMStaticInfo());
            await Task.Run(() => system.GetDiskStaticInfo());

            await interaction.RespondAsync(embed: (await GetMessageEmbed(system)).Build());
        }

        private static async Task SendServerEmbed(SocketInteraction interaction, string message, Color color, string serverId, string serverStatus,
            string serverName)
        {
            EmbedBuilder embed = new() { Color = color };
            embed.AddField("ID", serverId, inline: true);
            embed.AddField("狀態", serverStatus, inline: true);
            embed.AddField("伺服器名稱", serverName, inline: true);

            await interaction.FollowupAsync(text:message, embed: embed.Build());
        }

        private static string GetProgressBar(double progress)
        {
            const int MAX_BLOCK = 23;
            string display = $" {(int)progress}% ";

            int startIndex = (MAX_BLOCK / 2) - (display.Length / 2);
            string progressBar = string.Concat(Enumerable.Repeat("█", (int)(progress / 100 * MAX_BLOCK)))
                .PadRight(MAX_BLOCK).Remove(startIndex, display.Length).Insert(startIndex, display);

            return $"**`{progressBar}`**";
        }

        private static string GetActivePlayersString(int activePlayers)
        {
            const int MAX_BLOCK = 23;
            string display = $" {activePlayers} ";

            int startIndex = (MAX_BLOCK / 2) - (display.Length / 2);
            string activePlayersString = string.Concat(Enumerable.Repeat(" ", MAX_BLOCK))
                .Remove(startIndex, display.Length).Insert(startIndex, display);

            return $"**`{activePlayersString}`**";
        }

        private static async Task<(int, int, int)> GetGameServerDashBoardDetails() {
            return Application.Current == null
                ? (0, 0, 0)
                : await Application.Current.Dispatcher.Invoke(async () => {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                return (WindowsGSM.GetServerCount(), WindowsGSM.GetStartedServerCount(),
                    WindowsGSM.GetActivePlayers());
            });
        }

        private static async Task<EmbedBuilder> GetMessageEmbed(SystemMetrics system)
        {
            EmbedBuilder embed = new() {
                Title = ":small_orange_diamond: 系統指標",
                Description = $"主機名稱: {Environment.MachineName}",
                Color = Color.Blue
            };

            embed.AddField("CPU", GetProgressBar(await Task.Run(() => SystemMetrics.GetCPUUsage())), true);
            double ramUsage = await Task.Run(() => system.GetRAMUsage());
            embed.AddField("記憶體: " + SystemMetrics.GetMemoryRatioString(ramUsage, system.RAMTotalSize),
                GetProgressBar(ramUsage), true);
            double diskUsage = await Task.Run(() => system.GetDiskUsage());
            embed.AddField("硬碟: " + SystemMetrics.GetDiskRatioString(diskUsage, system.DiskTotalSize),
                GetProgressBar(diskUsage), true);

            (int serverCount, int startedCount, int activePlayers) = await GetGameServerDashBoardDetails();
            embed.AddField($"伺服器: {serverCount}/{MainWindow.MAX_SERVER}",
                GetProgressBar(serverCount * 100 / MainWindow.MAX_SERVER), true);
            embed.AddField($"在線: {startedCount}/{serverCount}",
                GetProgressBar((serverCount == 0) ? 0 : startedCount * 100 / serverCount), true);
            embed.AddField("活躍玩家", GetActivePlayersString(activePlayers), true);

            embed.WithFooter(new EmbedFooterBuilder()
                .WithIconUrl("https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM.png")
                .WithText($"WindowsGSM {MainWindow.WGSM_VERSION} | 系統指標"));
            embed.WithCurrentTimestamp();

            return embed;
        }

        private static bool IsStarted(MainWindow.ServerStatus serverStatus)
        {
            return serverStatus == MainWindow.ServerStatus.Started || serverStatus == MainWindow.ServerStatus.Starting;
        }
    }
}