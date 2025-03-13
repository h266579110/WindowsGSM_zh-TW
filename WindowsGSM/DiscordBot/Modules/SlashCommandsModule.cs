﻿using System;
using System.Threading.Tasks;
using Discord.Interactions;
using WindowsGSM.DiscordBot.Preconditions;

namespace WindowsGSM.DiscordBot.Modules
{
    [RequireAdmin]
    public class SlashCommandsModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Actions _actions = new Actions();

        // [RequiredRole] TODO implement role based access
        [SlashCommand("list", "List all servers")]
        public async Task ListCommand()
        {
            await Context.Interaction.RespondAsync(embed: await Actions.GetServerList(Context.User.Id.ToString()));
            Console.WriteLine($@"list-servers executed by {Context.User.Username}");
        }

        [SlashCommand("check", "Check server permissions")]
        public async Task CheckCommand()
        {
            await Context.Interaction.RespondAsync(await Actions.GetServerPermissions(Context.User.Id.ToString()));
            Console.WriteLine($@"check executed by {Context.User.Username}");
        }

        [SlashCommand("stats", "Get server stats")]
        public async Task StatsCommand()
        {
            await _actions.GetServerStats(Context.Interaction);
            Console.WriteLine($@"stats executed by {Context.User.Username}");
        }

        [SlashCommand("start", "Start a server")]
        public async Task StartCommand([Summary("server"), Autocomplete] string serverId)
        {
            await Context.Interaction.DeferAsync();
            await _actions.StartServer(Context.Interaction, serverId);
            Console.WriteLine($@"start server {serverId} executed by {Context.User.Username}");
        }

        [SlashCommand("stop", "Stop a server")]
        public async Task StopCommand([Summary("server"), Autocomplete] string serverId)
        {
            await Context.Interaction.DeferAsync();
            await _actions.StopServer(Context.Interaction, serverId);
            Console.WriteLine($@"stop server {serverId} executed by {Context.User.Username}");
        }

        [SlashCommand("stopAll", "Stops all server")]
        public async Task StopAllCommand()
        {
            await Context.Interaction.DeferAsync();
            await Actions.StopAllServer(Context.Interaction);
            Console.WriteLine($@"stoped all servers executed by {Context.User.Username}");
        }

        [SlashCommand("restart", "Restart a server")]
        public async Task RestartCommand([Summary("server"), Autocomplete] string serverId)
        {
            await Context.Interaction.DeferAsync();
            await _actions.RestartServer(Context.Interaction, serverId);
            Console.WriteLine($@"restart server {serverId} executed by {Context.User.Username}");
        }

        [SlashCommand("send", "Send a server command")]
        public async Task RestartCommand([Summary("server"), Autocomplete] string serverId, string command)
        {
            await Context.Interaction.DeferAsync();
            await Actions.SendServerCommand(Context.Interaction, serverId, command);
            Console.WriteLine($@"send server {serverId} command [{command}] executed by {Context.User.Username}");
        }

        [SlashCommand("sendR", "Send a server command and receives the return from it")]
        public async Task RestartCommandWithResponse([Summary("server"), Autocomplete] string serverId, string command)
        {
            await Context.Interaction.DeferAsync();
            await Actions.SendServerCommand(Context.Interaction, serverId, command);
            Console.WriteLine($@"sendR server {serverId} command [{command}] executed by {Context.User.Username}");
        }

        [SlashCommand("update", "Update a server")]
        public async Task UpdateCommand([Summary("server"), Autocomplete] string serverId)
        {
            await Context.Interaction.DeferAsync();
            await Actions.UpdateServer(Context.Interaction, serverId);
            Console.WriteLine($@"update server {serverId} executed by {Context.User.Username}");
        }

        [SlashCommand("backup", "Backup a server")]
        public async Task BackupCommand([Summary("server"), Autocomplete] string serverId)
        {
            await Context.Interaction.DeferAsync();
            await Actions.BackupServer(Context.Interaction, serverId);
            Console.WriteLine($@"backup server {serverId} executed by {Context.User.Username}");
        }
    }
}