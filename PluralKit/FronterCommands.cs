using Discord.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Discord;
using Microsoft.Extensions.Logging;

namespace Jules.PluralKit
{
    public class FronterCommands : ModuleBase<SocketCommandContext>
    {
        private ILogger logger;
        private PluralKitAPIService pk;
        private FronterCache cache;

        public FronterCommands(
            ILogger<FronterCommands> logger,
            PluralKitAPIService pk, 
            FronterCache cache)
        {
            this.logger = logger;
            this.pk = pk;
            this.cache = cache;
        }

        [Command("switch")]
        [Alias("s")]
        public async Task SwitchAsync(string memberName)
        {
            if (string.IsNullOrEmpty(memberName))
            {
                throw new ArgumentNullException(nameof(memberName));
            }
            var memberNameUpper = memberName.ToUpperInvariant();
            var user = Context.User;

            if (user.IsBot || user.IsWebhook) return;

            var system = await pk.GetSystem(user.Id);
            if (system == null)
            {
                await ReplyAsync($"You are not registered as a system. {user.Mention}");
                return;
            }

            if (memberNameUpper == "OUT")
            {
                cache.ClearFronter(user.Id);
                logger.LogInformation($"{user.Username} | cleared fronter.");
                return;
            }

            var members = await pk.GetMembers(system.id);
            if (members == null)
            {
                await ReplyAsync($"{user.Mention} -> You have no system members registered.");
                return;
            }

            var member = members.SingleOrDefault(m => m.name.ToUpperInvariant() == memberNameUpper);
            if (member == null)
            {
                await ReplyAsync($"{user.Mention} -> Can not find a member named {memberName}.");
                return;
            }

            string nameWithTag = $"{member.name} {system.tag}";
            cache.SetFronter(user.Id, nameWithTag, member.avatar_url);
            logger.LogInformation($"{user.Username} | {memberName} is now fronting");
        }

    }
}
