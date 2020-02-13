using Discord.Commands;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Jules.PluralKit
{
    public class FronterCommands : ModuleBase<SocketCommandContext>
    {
        private ILogger logger;
        private JulesConfig config;
        private PluralKitAPIService pk;
        private FronterCache cache;

        public FronterCommands(
            ILogger<FronterCommands> logger,
            JulesConfig config,
            PluralKitAPIService pk,
            FronterCache cache)
        {
            this.logger = logger;
            this.config = config;
            this.pk = pk;
            this.cache = cache;
        }

        [Command("switch")]
        [Alias("s")]
        public async Task SwitchAsync(params string[] memberNames)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (memberNames.Length == 0)
                {
                    throw new ArgumentNullException(nameof(memberNames));
                }
                var user = Context.User;

                if (user.IsBot || user.IsWebhook)
                {
                    return;
                }

                var system = await this.pk.GetSystem(user.Id);
                if (system == null)
                {
                    await ReplyAsync($"You are not registered as a system. {user.Mention}");
                    return;
                }

                var members = await this.pk.GetMembers(system.id);
                if (members == null)
                {
                    await ReplyAsync($"{user.Mention} -> You have no system members registered.");
                    return;
                }

                if (memberNames.Length == 1)
                {
                    string memberNameUpper = memberNames[0].ToUpperInvariant();

                    if (memberNameUpper == "OUT")
                    {
                        this.cache.ClearFronter(user.Id);
                        this.logger.LogInformation($"{user.Username} | cleared fronter.");
                        return;
                    }

                    var member = members.SingleOrDefault(m => m.name.ToUpperInvariant() == memberNameUpper);
                    if (member == null)
                    {
                        await ReplyAsync($"{user.Mention} -> Can not find a member named {memberNames[0]}.");
                        return;
                    }

                    string nameWithTag = $"{member.name} {system.tag}";
                    this.cache.SetFronter(user.Id, nameWithTag, member.avatar_url);
                    this.logger.LogInformation($"{user.Username} | {memberNames[0]} is now fronting");
                }
                else
                {
                    var memberNamesSet = memberNames.Select(m => m.ToUpperInvariant()).ToHashSet();
                    var membersSubset = members.Where(m => memberNamesSet.Contains(m.name.ToUpperInvariant())).ToArray();
                    if (membersSubset.Length != memberNames.Length)
                    {
                        await ReplyAsync($"{user.Mention} -> Can not find one or more of the specified members.");
                        return;
                    }

                    // combine alter names and avatars
                    var avatarUrl = new Uri("https://i.imgur.com/7okP8zX.png");
                    string nameWithTag = "";
                    List<Bitmap> avatars = new List<Bitmap>();
                    foreach (var name in memberNames)
                    {
                        var member = membersSubset.Single(m => m.name.ToUpperInvariant() == name.ToUpperInvariant());
                        nameWithTag += member.name + " + ";

                        if (member.avatar_url != null)
                        {
                            try
                            {
                                avatarUrl = member.avatar_url;
                                var request = WebRequest.CreateHttp(member.avatar_url);
                                using (var response = request.GetResponse())
                                {
                                    var image = new Bitmap(response.GetResponseStream());
                                    avatars.Add(image);
                                }
                            }
                            catch (Exception)
                            {
                                await ReplyAsync("error downloading avatar...");
                                return;
                            }
                        }
                    }
                    nameWithTag = nameWithTag.Remove(nameWithTag.Length - 2);
                    nameWithTag += system.tag;


                    // combine avatars
                    if (avatars.Count > 1)
                    {
                        using (var bitmap = new Bitmap(80, 80))
                        {
                            if (avatars.Count == 2)
                            {
                                using (var g = Graphics.FromImage(bitmap))
                                {
                                    g.DrawImage(avatars[0],
                                        new Rectangle(0, 0, 40, 80),
                                        new Rectangle(0, 0, avatars[0].Width / 2, avatars[0].Height),
                                        GraphicsUnit.Pixel);
                                    g.DrawImage(avatars[1],
                                        new Rectangle(40, 0, 40, 80),
                                        new Rectangle(avatars[1].Width / 2, 0, avatars[1].Width / 2, avatars[1].Height),
                                        GraphicsUnit.Pixel);
                                }
                            }
                            else if (avatars.Count >= 3)
                            {
                                using (var g = Graphics.FromImage(bitmap))
                                {
                                    g.DrawImage(avatars[0],
                                    new Rectangle(0, 0, 40, 40),
                                    new Rectangle(0, 0, avatars[0].Width / 2, avatars[0].Height / 2),
                                    GraphicsUnit.Pixel);
                                    g.DrawImage(avatars[1],
                                        new Rectangle(40, 0, 40, 40),
                                        new Rectangle(avatars[1].Width / 2, 0, avatars[1].Width / 2, avatars[1].Height / 2),
                                        GraphicsUnit.Pixel);
                                    g.DrawImage(avatars[2],
                                        new Rectangle(0, 40, 40, 40),
                                        new Rectangle(0, avatars[2].Height / 2, avatars[2].Width / 2, avatars[2].Height / 2),
                                        GraphicsUnit.Pixel);
                                    if (avatars.Count >= 4)
                                    {
                                        g.DrawImage(avatars[3],
                                            new Rectangle(40, 40, 40, 40),
                                            new Rectangle(avatars[3].Width / 2, avatars[3].Height / 2, avatars[3].Width / 2, avatars[3].Height / 2),
                                            GraphicsUnit.Pixel);
                                    }
                                }
                            }

                            try
                            {
                                var uri = "https://api.imgur.com/3/upload";
                                var request = WebRequest.CreateHttp(new Uri(uri));
                                request.Method = "POST";
                                request.Headers.Add("Authorization", $"Client-ID {this.config.ImgurId}");
                                using (var stream = request.GetRequestStream())
                                {
                                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                                    stream.Close();
                                }
                                //request.ContentLength = byteArray.Length;
                                using (var response = request.GetResponse())
                                using (var reader = new StreamReader(response.GetResponseStream()))
                                {
                                    var text = await reader.ReadToEndAsync();
                                    var json = JObject.Parse(text);
                                    if (json != null)
                                    {
                                        var token = json.SelectToken("data.link");
                                        if (token != null)
                                        {
                                            avatarUrl = new Uri(token.ToString());
                                        }
                                        else
                                        {
                                            await ReplyAsync("error posting avatar...");
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        await ReplyAsync("error posting avatar");
                                        return;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                await ReplyAsync("exception posting avatar");
                            }
                        }
                    }

                    this.cache.SetFronter(user.Id, nameWithTag, avatarUrl);
                    this.logger.LogInformation($"{user.Username} | {memberNames} are now co-fronting");
                }
            }
        }
    }
}
