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

namespace Jules
{
    public class BasicCommands : ModuleBase<SocketCommandContext>
    {
        private JulesConfig config;

        public BasicCommands(JulesConfig config)
        {
            this.config = config;
        }

        [Command("calm")]
        [Summary("posts calming image")]
        public async Task CalmAsync()
        {
            await Context.Channel.SendMessageAsync("", false, new EmbedBuilder()
                .WithImageUrl("https://i.imgur.com/we3ItB9.gif")
                .Build());
        }

        [Command("breathe")]
        [Alias("breath", "breth", "inhale", "exhale")]
        [Summary("posts breathing image")]
        public async Task BreatheAsync()
        {
            await Context.Channel.SendMessageAsync("", false, new EmbedBuilder()
                .WithImageUrl("https://i.imgur.com/D8OKV1E.gif")
                .Build());
        }

        [Command("catbreathe")]
        [Alias("catbreath", "catbreth", "breathecat", "breathcat", "brethcat")]
        [Summary("posts breathing image (with a cute cat)")]
        public async Task CatBreatheAsync()
        {
            await Context.Channel.SendMessageAsync("", false, new EmbedBuilder()
                .WithImageUrl("https://i.imgur.com/aAxZ0SO.gif")
                .Build());
        }

        [Command("ground")]
        [Alias("grounding")]
        [Summary("posts grounding image")]
        public async Task GroundAsync()
        {
            await Context.Channel.SendMessageAsync("", false, new EmbedBuilder()
                .WithImageUrl("https://i.imgur.com/tC43aP1.jpg")
                .Build());
        }

        [Command("cat")]
        [Alias("kat", "kitty", "kitties", "kittys", "meow")]
        [Summary("posts a random cat")]
        public async Task CatAsync()
        {
            using (Context.Channel.EnterTypingState())
            {
                try
                {
                    var uri = $"https://thecatapi.com/api/images/get?api_key={config.CatAPIKey}&format=xml";
                    var request = WebRequest.CreateHttp(new Uri(uri));
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var text = await reader.ReadToEndAsync();
                        var xml = XDocument.Parse(text);
                        var url = xml.Descendants("url").SingleOrDefault();
                        if (url != null)
                        {
                            await Context.Channel.SendMessageAsync("", false, new EmbedBuilder().WithImageUrl(url.Value).Build());
                        }
                    }
                }
                catch (Exception)
                {
                    await ReplyAsync("cat overflow exception... too many cats in liquid state");
                }
            }
        }

        [Command("dog")]
        [Alias("dogo", "doggo", "dogy", "doggy", "dawg", "doge", "dogey", "dego", "deg", "pup", "puper", "pupper", "pupr", "puppr", "pupy", "puppy")]
        [Summary("posts a random dog")]
        public async Task DogAsync()
        {
            using (Context.Channel.EnterTypingState())
            {
                try
                {
                    var uri = $"https://dog.ceo/api/breeds/image/random";
                    var request = WebRequest.CreateHttp(new Uri(uri));
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var text = await reader.ReadToEndAsync();
                        var json = JObject.Parse(text);
                        if (json != null)
                        {
                            var token = json.SelectToken("message");
                            if (token != null)
                            {
                                await Context.Channel.SendMessageAsync("", false, new EmbedBuilder().WithImageUrl(token.ToString()).Build());
                            }
                            else
                            {
                                await ReplyAsync("dog not found exception... where did doggo go? >.<");
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    await ReplyAsync("dog not found exception... where did doggo go? >.<");
                }
            }
        }

        [Command("define")]
        [Summary("posts definition of a word")]
        public async Task SayAsync([Remainder][Summary("the word(s) to lookup")] string word)
        {
            using (Context.Channel.EnterTypingState())
            {
                try
                {
                    var uri = $"https://mashape-community-urban-dictionary.p.mashape.com/define?term={word}";
                    var request = WebRequest.CreateHttp(new Uri(uri));
                    request.Headers.Add("X-Mashape-Key", config.MashapeKey);
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var text = await reader.ReadToEndAsync();
                        var json = JObject.Parse(text);
                        if (json != null)
                        {
                            var token = json.SelectToken("list[0].definition");
                            if (token != null)
                            {
                                var definition = token.Value<string>();
                                var words = json.SelectToken("list[0].word");
                                var example = json.SelectToken("list[0].example");
                                var message = $">>> Definition of **{words}** ```\r\n{definition}```\r\n Example\r\n```\r\n{example}```";
                                await ReplyAsync(message);
                            }
                            else
                            {
                                await ReplyAsync("error token sucks or something");
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    await ReplyAsync("dictionary broke");
                }
            }
        }
    }
}
