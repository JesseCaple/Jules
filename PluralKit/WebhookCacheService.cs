using Discord;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Jules.PluralKit
{
    public sealed class WebhookCacheService
    {
        public static readonly string WebhookName = "PluralKit Proxy Webhook";

        private IDiscordClient _client;
        private ConcurrentDictionary<ulong, Lazy<Task<IWebhook>>> _webhooks;
        private ILogger _logger;

        public WebhookCacheService(
            IDiscordClient client, 
            ILogger<WebhookCacheService> logger)
        {
            _client = client;
            _logger = logger;
            _webhooks = new ConcurrentDictionary<ulong, Lazy<Task<IWebhook>>>();
        }

        public async Task<IWebhook> GetWebhook(ulong channelId)
        {
            var channel = await _client.GetChannelAsync(channelId) as ITextChannel;
            if (channel == null) return null;
            return await GetWebhook(channel);
        }

        public async Task<IWebhook> GetWebhook(ITextChannel channel)
        {
            // We cache the webhook through a Lazy<Task<T>>, this way we make sure to only create one webhook per channel
            // If the webhook is requested twice before it's actually been found, the Lazy<T> wrapper will stop the
            // webhook from being created twice.
            var lazyWebhookValue =
                _webhooks.GetOrAdd(channel.Id, new Lazy<Task<IWebhook>>(() => GetOrCreateWebhook(channel)));

            // It's possible to "move" a webhook to a different channel after creation
            // Here, we ensure it's actually still pointing towards the proper channel, and if not, wipe and refetch one.
            var webhook = await lazyWebhookValue.Value;
            if (webhook.ChannelId != channel.Id) return await InvalidateAndRefreshWebhook(webhook);
            return webhook;
        }

        public async Task<IWebhook> InvalidateAndRefreshWebhook(IWebhook webhook)
        {
            _logger.LogInformation("Refreshing webhook for channel {Channel}", webhook.ChannelId);

            _webhooks.TryRemove(webhook.ChannelId, out _);
            return await GetWebhook(webhook.Channel);
        }

        private async Task<IWebhook> GetOrCreateWebhook(ITextChannel channel)
        {
            _logger.LogDebug("Webhook for channel {Channel} not found in cache, trying to fetch", channel.Id);
            return await FindExistingWebhook(channel) ?? await DoCreateWebhook(channel);
        }

        private async Task<IWebhook> FindExistingWebhook(ITextChannel channel)
        {
            _logger.LogDebug("Finding webhook for channel {Channel}", channel.Id);
            try
            {
                return (await channel.GetWebhooksAsync()).FirstOrDefault(IsWebhookMine);
            }
            catch (HttpRequestException e)
            {
                _logger.LogWarning(e, "Error occurred while fetching webhook list");
                // This happens sometimes when Discord returns a malformed request for the webhook list
                // Nothing we can do than just assume that none exist and return null.
                return null;
            }
        }

        private Task<IWebhook> DoCreateWebhook(ITextChannel channel)
        {
            _logger.LogInformation("Creating new webhook for channel {Channel}", channel.Id);
            return channel.CreateWebhookAsync(WebhookName);
        }

        private bool IsWebhookMine(IWebhook arg) => arg.Creator.Id == _client.CurrentUser.Id && arg.Name == WebhookName;

        public int CacheSize => _webhooks.Count;
    }
}
