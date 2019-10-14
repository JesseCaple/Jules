using Discord;
using Discord.Net;
using Discord.Webhook;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Jules.PluralKit
{
    public sealed class WebhookExecutorService : IDisposable
    {
        private WebhookCacheService _webhookCache;
        private IMemoryCache _cache;
        private ILogger _logger;
        private HttpClient _client;

        public WebhookExecutorService(
            IMemoryCache cache,
            WebhookCacheService webhookCache, 
            ILogger<WebhookExecutorService> logger)
        {
            _cache = cache;
            _webhookCache = webhookCache;
            _logger = logger;
            _client = new HttpClient();
        }

#pragma warning disable CA1054 // Uri parameters should not be strings
        public async Task<ulong> ExecuteWebhook(ITextChannel channel, string name, string avatarUrl, string content, IAttachment attachment)

        {
            _logger.LogDebug("Invoking webhook in channel {Channel}", channel.Id);

            // Get a webhook, execute it
            var webhook = await _webhookCache.GetWebhook(channel);
            var id = await ExecuteWebhookInner(webhook, name, avatarUrl, content, attachment);

            // Log the relevant metrics
            _logger.LogInformation("Invoked webhook {Webhook} in channel {Channel}", webhook.Id, channel.Id);

            return id;
        }

        private async Task<ulong> ExecuteWebhookInner(IWebhook webhook, string name, string avatarUrl, string content,
            IAttachment attachment, bool hasRetried = false)
        {
            var client = await GetClientFor(webhook);

            try
            {
                // If we have an attachment, use the special SendFileAsync method
                if (attachment != null)
                    using (var attachmentStream = await _client.GetStreamAsync(new Uri(attachment.Url)))
                        return await client.SendFileAsync(
                            attachmentStream, 
                            attachment.Filename, 
                            content,
                            username: FixClyde(name),
                            avatarUrl: avatarUrl);

                // Otherwise, send normally
                return await client.SendMessageAsync(content, username: FixClyde(name), avatarUrl: avatarUrl);
            }
            catch (HttpException e)
            {
                // If we hit an error, just retry (if we haven't already)
                if (e.DiscordCode == 10015 && !hasRetried) // Error 10015 = "Unknown Webhook"
                {
                    _logger.LogWarning(e, "Error invoking webhook {Webhook} in channel {Channel}", webhook.Id, webhook.ChannelId);
                    return await ExecuteWebhookInner(await _webhookCache.InvalidateAndRefreshWebhook(webhook), name, avatarUrl, content, attachment, hasRetried: true);
                }

                throw;
            }
        }
#pragma warning restore CA1054 // Uri parameters should not be strings

        private async Task<DiscordWebhookClient> GetClientFor(IWebhook webhook)
        {
            _logger.LogDebug("Looking for client for webhook {Webhook} in cache", webhook.Id);
            return await _cache.GetOrCreateAsync($"_webhook_client_{webhook.Id}",
                (entry) => MakeCachedClientFor(entry, webhook));
        }

        private Task<DiscordWebhookClient> MakeCachedClientFor(ICacheEntry entry, IWebhook webhook)
        {
            _logger.LogInformation("Client for {Webhook} not found in cache, creating", webhook.Id);

            // Define expiration for the client cache
            // 10 minutes *without a query* and it gets yoten
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            // IMemoryCache won't automatically dispose of its values when the cache gets evicted
            // so we register a hook to do so here.
            entry.RegisterPostEvictionCallback((key, value, reason, state) => (value as IDisposable)?.Dispose());

            // DiscordWebhookClient has a sync network call in its constructor (!!!)
            // and we want to punt that onto a task queue, so we do that.
            return Task.Run(async () =>
            {
                try
                {
                    return new DiscordWebhookClient(webhook);
                }
                catch (InvalidOperationException)
                {
                    // TODO: does this leak stuff inside the DiscordWebhookClient created above?

                    // Webhook itself was found in cache, but has been deleted on the channel
                    // We request a new webhook instead
                    return new DiscordWebhookClient(await _webhookCache.InvalidateAndRefreshWebhook(webhook));
                }

            });
        }

        private static string FixClyde(string name)
        {
            // Check if the name contains "Clyde" - if not, do nothing
            var match = Regex.Match(name, "clyde", RegexOptions.IgnoreCase);
            if (!match.Success) return name;

            // Put a hair space (\u200A) between the "c" and the "lyde" in the match to avoid Discord matching it
            // since Discord blocks webhooks containing the word "Clyde"... for some reason. /shrug
            return name.Substring(0, match.Index + 1) + '\u200A' + name.Substring(match.Index + 1);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
