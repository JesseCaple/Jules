using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jules.PluralKit
{
    public class FronterProxyService
    {
        private ILogger<FronterProxyService> logger;
        private FronterCache fronterCache;
        private WebhookExecutorService webhookExecutor;

        public FronterProxyService(
            ILogger<FronterProxyService> logger,
            FronterCache cache,
            WebhookExecutorService webhookExecutor)
        {
            this.logger = logger;
            this.fronterCache = cache;
            this.webhookExecutor = webhookExecutor;
        }

        public async Task<bool> HandleMessage(SocketMessage message)
        {
            if (message == null
                || !(message.Channel is ITextChannel)
                || (message.Content.Length == 0 && message.Attachments.Count == 0))
            {
                return false;
            }

            var fronter = fronterCache.GetFronter(message.Author.Id);
            if (fronter == null)
            {
                return false;
            }

            // TODO: ensure they have permission to send messages here
            // TODO: sanatize everyone if they don't have permission

            // Execute the webhook itself
            var hookMessageId = await webhookExecutor.ExecuteWebhook(
                (ITextChannel)message.Channel,
                fronter.NameWithTag, fronter.AvatarUrl,
                message.Content,
                message.Attachments.FirstOrDefault()
            );

            await Task.Delay(50);
            try
            {
                await message.DeleteAsync();
            }
            catch (HttpException) {}
            await Task.Delay(950);
            try
            {
                await message.DeleteAsync();
            }
            catch (HttpException) {}
            return true;
        }

    }
}
