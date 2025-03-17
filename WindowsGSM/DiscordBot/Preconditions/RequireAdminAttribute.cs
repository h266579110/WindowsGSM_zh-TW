using Discord;
using Discord.Interactions;
using System;
using System.Threading.Tasks;

namespace WindowsGSM.DiscordBot.Preconditions
{
    public class RequireAdminAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            System.Collections.Generic.List<string> adminIds = Configs.GetBotAdminIds();
            return !adminIds.Contains(context.User.Id.ToString())
                ? PreconditionResult.FromError("You don't have permission to use this command.")
                : PreconditionResult.FromSuccess();
        }
    }
}