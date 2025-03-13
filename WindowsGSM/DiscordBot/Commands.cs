using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WindowsGSM.Functions;

namespace WindowsGSM.DiscordBot {
    class Commands
    {
        private readonly DiscordSocketClient _client;

        public Commands(DiscordSocketClient client)
        {
            _client = client;
            _client.MessageReceived += CommandReceivedAsync;
        }

        private async Task CommandReceivedAsync(SocketMessage message)
        {
            // The bot should never respond to itself.
            if (message.Author.Id == _client.CurrentUser.Id) { return; }

            // Return if the author is not admin
            List<string> adminIds = Configs.GetBotAdminIds();
            if (!adminIds.Contains(message.Author.Id.ToString())) { return; }

            // Return if the message is not WindowsGSM prefix
            string prefix = Configs.GetBotPrefix();
            int commandLen = prefix.Length + 4;
            if (message.Content.Length < commandLen) { return; }
            if (message.Content.Length == commandLen && message.Content.ToLower().Trim() == $"{prefix}wgsm".ToLower().Trim())
            {
                await SendHelpEmbed(message);
                return;
            }

            if (message.Content.Length >= commandLen + 1 && message.Content[..(commandLen + 1)].ToLower().Trim() == $"{prefix}wgsm ".ToLower().Trim())
            {
                // Remote Actions
                string[] args = message.Content.Split([' '], 2);
                string[] splits = args[1].Split(' ');

                switch (splits[0])
                {
                    case "start":
                    case "stop":
                    case "stopAll":
                    case "restart":
                    case "send":
                    case "sendR":
                    case "list":
                    case "check":
                    case "backup":
                    case "update":
                    case "stats":
                    case "players":
                    case "serverStats":
                        List<string> serverIds = Configs.GetServerIdsByAdminId(message.Author.Id.ToString());
                        if (splits[0] == "check")
                        {
                            await message.Channel.SendMessageAsync(
                                serverIds.Contains("0") ?
                                "你擁有所有權限.\n指令: `check`, `list`, `start`, `stop`, `stopAll`, `restart`, `send`, `sendR`, `backup`, `update`, `players`, `stats`" :
                                $"你在伺服器 (`{string.Join(",", [.. serverIds])}`) 擁有權限\n指令: `check`, `start`, `stop`, `restart`, `send`, `sendR`, `backup`, `update`, `players`, `stats`");
                            break;
                        }

                        if (splits[0] == "list" && serverIds.Contains("0"))
                        {
                            await Action_List(message);
                        }
                        else if (splits[0] == "stopAll" && serverIds.Contains("0"))
                        {
                            await Action_StopAll(message);
                        }
                        else if (splits[0] != "list" && (serverIds.Contains("0") || serverIds.Contains(splits[1])))
                        {
                            switch (splits[0])
                            {
                                case "start": await Action_Start(message, args[1]); break;
                                case "stop": await Action_Stop(message, args[1]); break;
                                case "restart": await Action_Restart(message, args[1]); break;
                                case "send": await Action_SendCommand(message, args[1]); break;
                                case "sendR": await Action_SendCommand(message, args[1], true); break;
                                case "backup": await Action_Backup(message, args[1]); break;
                                case "update": await Action_Update(message, args[1]); break;
                                case "players": await Action_PlayerList(message, args[1]); break;
                                case "serverStats": await Action_GameServerStats(message, args[1]); break;
                                case "stats": await Action_Stats(message); break;
                            }
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync("你沒有權限存取 <:gijoe_you:1077950712476074046>");
                        }
                        break;
                    default: await SendHelpEmbed(message); break;
                }
            }
        }

        private static async Task Action_PlayerList(SocketMessage message, string command)
        {
            EmbedBuilder embed = new() { };
            string[] args = command.Split(' ');
            if (args.Length >= 2 && int.TryParse(args[1], out int i))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        List<GameServer.Query.PlayerData> playerList = WindowsGSM.GetServerTableById(args[1]).PlayerList;
                        foreach (GameServer.Query.PlayerData player in playerList)
                        {
                            embed.AddField($"玩家 {player.Id}", $"名稱: {player.Name}; 分數:{player.Score}; 已連線時間:{player.TimeConnected?.TotalMinutes.ToString("#.##")}", inline: true);
                        }
                    }
                    if (embed.Fields.Count == 0)
                    {
                        embed.AddField("玩家資料", "現在沒有玩家資料可用!");
                    }
                });
            }
            else
            {
                embed.AddField("玩家資料", "你的查詢出了一些問題!");
            }
            await message.Channel.SendMessageAsync(embed: embed.Build());
        }

        private static async Task Action_List(SocketMessage message)
        {
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;

                List<(string, string, string)> list = WindowsGSM.GetServerList();

                string ids = string.Empty;
                string status = string.Empty;
                string servers = string.Empty;

                foreach ((string id, string state, string server) in list)
                {
                    ids += $"`{id}`\n";
                    status += $"`{state}`\n";
                    servers += $"`{server}`\n";
                }

                EmbedBuilder embed = new() { Color = Color.Teal };
                embed.AddField("ID", ids, inline: true);
                embed.AddField("狀態", status, inline: true);
                embed.AddField("伺服器名稱", servers, inline: true);

                await message.Channel.SendMessageAsync(embed: embed.Build());
            });
        }

        private static async Task Action_Start(SocketMessage message, string command)
        {
            string[] args = command.Split(' ');
            if (args.Length == 2 && int.TryParse(args[1], out int i))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(args[1]);
                        if (serverStatus == MainWindow.ServerStatus.Stopped)
                        {
                            bool started = await WindowsGSM.StartServerById(args[1], message.Author.Id.ToString(), message.Author.Username);
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) {(started ? "已啟動" : "啟動失敗")}.");
                        }
                        else if (serverStatus == MainWindow.ServerStatus.Started)
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 已經啟動");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 目前狀態為 {serverStatus}，無法啟動");
                        }

                        await SendServerEmbed(message, Color.Green, args[1], WindowsGSM.GetServerStatus(args[1]).ToString(), WindowsGSM.GetServerName(args[1]));
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 不存在");
                    }
                });
            }
            else
            {
                await message.Channel.SendMessageAsync($"使用方式： {Configs.GetBotPrefix()}wgsm start `<SERVERID>`");
            }
        }

        private static async Task Action_Stop(SocketMessage message, string command)
        {
            string[] args = command.Split(' ');
            if (args.Length == 2 && int.TryParse(args[1], out int i))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(args[1]);
                        if (serverStatus == MainWindow.ServerStatus.Started || serverStatus == MainWindow.ServerStatus.Starting)
                        {
                            bool started = await WindowsGSM.StopServerById(args[1], message.Author.Id.ToString(), message.Author.Username);
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) {(started ? "已停止" : "停止失敗")}.");
                        }
                        else if (serverStatus == MainWindow.ServerStatus.Stopped)
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 已經停止");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 目前狀態為 {serverStatus}，無法停止");
                        }

                        await SendServerEmbed(message, Color.Orange, args[1], WindowsGSM.GetServerStatus(args[1]).ToString(), WindowsGSM.GetServerName(args[1]));
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 不存在");
                    }
                });
            }
            else
            {
                await message.Channel.SendMessageAsync($"使用方式： {Configs.GetBotPrefix()}wgsm stop `<SERVERID>`");
            }
        }

        private static async Task Action_StopAll(SocketMessage message)
        {
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                List<(string, string, string)> serverList = WindowsGSM.GetServerList();
                foreach ((string, string, string) server in serverList)
                {
                    if (WindowsGSM.IsServerExist(server.Item1))
                    {
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(server.Item1);
                        if (serverStatus == MainWindow.ServerStatus.Started || serverStatus == MainWindow.ServerStatus.Starting)
                        {
                            bool started = await WindowsGSM.StopServerById(server.Item1, message.Author.Id.ToString(), message.Author.Username);
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {server.Item1}) {(started ? "已停止" : "停止失敗")}.");
                        }
                        else if (serverStatus == MainWindow.ServerStatus.Stopped)
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {server.Item1}) 已經停止");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {server.Item1}) 目前狀態為 {serverStatus}，無法停止");
                        }
                    }
                }
            });
        }

        private static async Task Action_Restart(SocketMessage message, string command)
        {
            string[] args = command.Split(' ');
            if (args.Length == 2 && int.TryParse(args[1], out int i))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(args[1]);
                        if (serverStatus == MainWindow.ServerStatus.Started || serverStatus == MainWindow.ServerStatus.Starting)
                        {
                            bool started = await WindowsGSM.RestartServerById(args[1], message.Author.Id.ToString(), message.Author.Username);
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) {(started ? "已重啟" : "重啟失敗")}.");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 目前狀態為 {serverStatus}，無法重啟");
                        }

                        await SendServerEmbed(message, Color.Blue, args[1], WindowsGSM.GetServerStatus(args[1]).ToString(), WindowsGSM.GetServerName(args[1]));
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 不存在");
                    }
                });
            }
            else
            {
                await message.Channel.SendMessageAsync($"使用方式： {Configs.GetBotPrefix()}wgsm restart `<SERVERID>`");
            }
        }

        private static async Task Action_SendCommand(SocketMessage message, string command, bool withResponse = false)
        {
            string[] args = command.Split(' ');
            if (args.Length >= 2 && int.TryParse(args[1], out int id))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(args[1]);
                        if (serverStatus == MainWindow.ServerStatus.Started || serverStatus == MainWindow.ServerStatus.Starting)
                        {
                            string sendCommand = command[(args[1].Length + 6)..].Trim();
                            string response = await WindowsGSM.SendCommandById(args[1], sendCommand, message.Author.Id.ToString(), message.Author.Username, withResponse ? 1000 : 0);
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) {(!string.IsNullOrWhiteSpace(response) ? "指令已送出" : "發送指令失敗")}. | `{sendCommand}`");
                            if (withResponse)
                            {
                                await SendMultiLog(message, response);
                            }
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 目前狀態為 {serverStatus}，無法發送指令");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 不存在");
                    }
                });
            }
            else
            {
                await message.Channel.SendMessageAsync($"使用方式： {Configs.GetBotPrefix()}wgsm send `<SERVERID>` `<COMMAND>`");
            }
        }

        public static async Task SendMultiLog(SocketMessage message, string response)
        {
            await message.Channel.SendMessageAsync($"LastLog:"); //read last log (2k is the limit for dc messages
            const int signsToSend = 1800;
            for (int i = 0; i < response.Length; i += signsToSend)
            {
                int len = i + signsToSend < response.Length ? signsToSend : response.Length - i;
                await message.Channel.SendMessageAsync($"```\n{response.Substring(i, len)}\n```"); //read last log (2k is the limit for dc messages
            }
        }

        private static async Task Action_Backup(SocketMessage message, string command)
        {
            string[] args = command.Split(' ');
            if (args.Length >= 2 && int.TryParse(args[1], out int i))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(args[1]);
                        if (serverStatus == MainWindow.ServerStatus.Stopped)
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 開始備份 - 這可能需要一些時間");
                            bool backuped = await WindowsGSM.BackupServerById(args[1], message.Author.Id.ToString(), message.Author.Username);
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) {(backuped ? "備份完成" : "備份失敗")}.");
                        }
                        else if (serverStatus == MainWindow.ServerStatus.Backuping)
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 正在備份");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 目前狀態為 {serverStatus}，無法備份");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 不存在");
                    }
                });
            }
            else
            {
                await message.Channel.SendMessageAsync($"使用方式： {Configs.GetBotPrefix()}wgsm backup `<SERVERID>`");
            }
        }

        private static async Task Action_Update(SocketMessage message, string command)
        {
            string[] args = command.Split(' ');
            if (args.Length >= 2 && int.TryParse(args[1], out int i))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(args[1]);
                        if (serverStatus == MainWindow.ServerStatus.Stopped)
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 開始更新 - 這可能需要一些時間");
                            bool updated = await WindowsGSM.UpdateServerById(args[1], message.Author.Id.ToString(), message.Author.Username);
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) {(updated ? "已更新" : "更新失敗")}.");
                        }
                        else if (serverStatus == MainWindow.ServerStatus.Updating)
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 正在更新");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 目前狀態為 {serverStatus}，無法更新");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 不存在");
                    }
                });
            }
            else
            {
                await message.Channel.SendMessageAsync($"使用方式： {Configs.GetBotPrefix()}wgsm update `<SERVERID>`");
            }
        }

        private static async Task Action_Stats(SocketMessage message)
        {
            SystemMetrics system = new();
            await Task.Run(() => system.GetCPUStaticInfo());
            await Task.Run(() => system.GetRAMStaticInfo());
            await Task.Run(() => system.GetDiskStaticInfo());

            await message.Channel.SendMessageAsync(embed: (await GetMessageEmbed(system)).Build());
        }

        private static async Task Action_GameServerStats(SocketMessage message, string command)
        {
            Console.WriteLine("executing gameserverstats");
            string[] args = command.Split(' ');
            if (args.Length == 2 && int.TryParse(args[1], out int i))
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    if (WindowsGSM.IsServerExist(args[1]))
                    {
                        Console.WriteLine("executing gameserverstats_ server exists");
                        MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(args[1]);
                        if (serverStatus == MainWindow.ServerStatus.Started)
                        {
                            ServerTable serverTable = WindowsGSM.GetServerTableById(args[1]);
                            await message.Channel.SendMessageAsync(embed: (await GetServerStatsMessage(serverTable)).Build());
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 目前狀態為 {serverStatus}，無法獲取資訊");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"伺服器 (ID: {args[1]}) 不存在");
                    }
                });
            }
            else
            {
                await message.Channel.SendMessageAsync($"使用方式： {Configs.GetBotPrefix()}wgsm restart `<SERVERID>`");
            }
        }


        private static async Task SendServerEmbed(SocketMessage message, Color color, string serverId, string serverStatus, string serverName)
        {
            EmbedBuilder embed = new() { Color = color };
            embed.AddField("ID", serverId, inline: true);
            embed.AddField("狀態", serverStatus, inline: true);
            embed.AddField("伺服器名稱", serverName, inline: true);

            await message.Channel.SendMessageAsync(embed: embed.Build());
        }

        private static async Task SendHelpEmbed(SocketMessage message)
        {
            EmbedBuilder embed = new() {
                Title = "可使用指令：",
                Color = Color.Teal
            };

            string prefix = Configs.GetBotPrefix();
            embed.AddField("指令", $"{prefix}wgsm check\n{prefix}wgsm list\n{prefix}wgsm stats\n{prefix}wgsm start <SERVERID>\n{prefix}wgsm stop <SERVERID>\n{prefix}wgsm restart <SERVERID>\n{prefix}wgsm update <SERVERID>\n{prefix}wgsm send <SERVERID> <COMMAND>\n{prefix}wgsm sendR <SERVERID> <COMMAND>\n{prefix}wgsm backup <SERVERID>\n{prefix}wgsm serverStats <SERVERID>\n{prefix}wgsm players <SERVERID>", inline: true);
            embed.AddField("說明", "檢查權限\n列出伺服器列表\n獲取主機系統指標\n使用 serverId 遠端啟動伺服器\n使用 serverId 遠端停止伺服器\n使用 serverId 遠端重啟伺服器\n使用 serverId 遠端更新伺服器\n發送指令到伺服器後台\n發送指令到伺服器後台並回傳結果\n使用 serverId 遠端備份伺服器\n使用 serverId 獲得伺服器資訊\n列出玩家列表，如果有的話", inline: true);

            await message.Channel.SendMessageAsync(embed: embed.Build());
        }

        private static string GetProgressBar(double progress)
        {
            // ▌ // ▋ // █ // Which one is the best?
            const int MAX_BLOCK = 23;
            string display = $" {(int)progress}% ";

            int startIndex = (MAX_BLOCK / 2) - (display.Length / 2);
            string progressBar = string.Concat(Enumerable.Repeat("█", (int)(progress / 100 * MAX_BLOCK))).PadRight(MAX_BLOCK).Remove(startIndex, display.Length).Insert(startIndex, display);

            return $"**`{progressBar}`**";
        }

        private static string GetActivePlayersString(int activePlayers)
        {
            const int MAX_BLOCK = 23;
            string display = $" {activePlayers} ";

            int startIndex = (MAX_BLOCK / 2) - (display.Length / 2);
            string activePlayersString = string.Concat(Enumerable.Repeat(" ", MAX_BLOCK)).Remove(startIndex, display.Length).Insert(startIndex, display);

            return $"**`{activePlayersString}`**";
        }

        private static async Task<(int, int, int)> GetGameServerDashBoardDetails()
        {
            return Application.Current != null
                ? await Application.Current.Dispatcher.Invoke(async () =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    return (WindowsGSM.GetServerCount(), WindowsGSM.GetStartedServerCount(), WindowsGSM.GetActivePlayers());
                })
                : (0, 0, 0);
        }


        private static async Task<EmbedBuilder> GetServerStatsMessage(ServerTable table)
        {
            EmbedBuilder embed = new() {
                Title = ":small_orange_diamond: 系統指標",
                Description = $"主機名稱: {Environment.MachineName}",
                Color = Color.Blue
            };

            embed.AddField("ID", table.ID, true);
            embed.AddField("伺服器名稱", table.Name, true);
            embed.AddField("伺服器 IP", table.IP, true);
            embed.AddField("伺服器公網 IP", MainWindow.GetPublicIP(), true);
            embed.AddField("玩家人數", table.Maxplayers, true);
            embed.WithCurrentTimestamp();

            return embed;
        }

        private static async Task<EmbedBuilder> GetMessageEmbed(SystemMetrics system)
        {
            EmbedBuilder embed = new() {
                Title = ":small_orange_diamond: 系統指標",
                Description = $"主機名稱: {Environment.MachineName}",
                Color = Color.Blue
            };

            embed.AddField("CPU", GetProgressBar(await Task.Run(() => system.GetCPUUsage())), true);
            double ramUsage = await Task.Run(() => system.GetRAMUsage());
            embed.AddField("記憶體: " + SystemMetrics.GetMemoryRatioString(ramUsage, system.RAMTotalSize), GetProgressBar(ramUsage), true);
            double diskUsage = await Task.Run(() => system.GetDiskUsage());
            embed.AddField("硬碟: " + SystemMetrics.GetDiskRatioString(diskUsage, system.DiskTotalSize), GetProgressBar(diskUsage), true);

            (int serverCount, int startedCount, int activePlayers) = await GetGameServerDashBoardDetails();
            embed.AddField($"伺服器: {serverCount}/{MainWindow.MAX_SERVER}", GetProgressBar(serverCount * 100 / MainWindow.MAX_SERVER), true);
            embed.AddField($"在線: {startedCount}/{serverCount}", GetProgressBar((serverCount == 0) ? 0 : startedCount * 100 / serverCount), true);
            embed.AddField("活耀玩家", GetActivePlayersString(activePlayers), true);

            embed.WithFooter(new EmbedFooterBuilder().WithIconUrl("https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM.png").WithText($"WindowsGSM {MainWindow.WGSM_VERSION} | 系統指標"));
            embed.WithCurrentTimestamp();

            return embed;
        }

    }
}
