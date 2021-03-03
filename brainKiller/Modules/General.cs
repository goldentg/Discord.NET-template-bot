﻿using System;
using System.Linq;
using System.Threading.Tasks;
using brainKiller.Common;
using brainKiller.Utilities;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace brainKiller.Modules
{
    public class General : ModuleBase<SocketCommandContext>
    {
        private readonly Images _images;
        private readonly ILogger<General> _logger;
        private readonly RanksHelper _ranksHelper;
        private readonly Servers _servers;
        private readonly CommandService _service;

        public General(ILogger<General> logger, Images images, RanksHelper ranksHelper, CommandService service,
            Servers servers)
        {
            _logger = logger;
            _images = images;
            _ranksHelper = ranksHelper;
            _service = service;
            _servers = servers;
        }

        [Command("ping", RunMode = RunMode.Async)]
        [Summary("Ping the bot")]
        public async Task Ping()
        {
            await Context.Channel.SendSuccessAsync("Ping", "Pong!");
        }

        [Command("info", RunMode = RunMode.Async)]
        [Summary("See information about a fellow member")]
        public async Task Info(SocketGuildUser user = null)
        {
            if (user == null)
            {
                var builder = new EmbedBuilder()
                    .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithDescription("see some info about yourself")
                    .WithColor(new Color(33, 176, 252))
                    .AddField("User ID", Context.User.Id, true)
                    .AddField("Account Creation Date", Context.User.CreatedAt.ToString("yyyy/MM/dd"), true)
                    .AddField("Date User Joined Server",
                        (Context.User as SocketGuildUser).JoinedAt.Value.ToString("yyyy/MM/dd"), true)
                    .AddField("Roles", string.Join(" ", (Context.User as SocketGuildUser).Roles.Select(x => x.Mention)))
                    .WithCurrentTimestamp();
                var embed = builder.Build();
                await Context.Channel.SendMessageAsync(null, false, embed);
            }
            else
            {
                var builder = new EmbedBuilder()
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                    .WithDescription($"see some info about {user.Username}")
                    .WithColor(new Color(33, 176, 252))
                    .AddField("User ID", user.Id, true)
                    .AddField("Account Creation Date", user.CreatedAt.ToString("yyyy/MM/dd"), true)
                    .AddField("Date User Joined Server", user.JoinedAt.Value.ToString("yyyy/MM/dd"), true)
                    .AddField("Roles", string.Join(" ", user.Roles.Select(x => x.Mention)))
                    .WithCurrentTimestamp();
                var embed = builder.Build();
                await Context.Channel.SendMessageAsync(null, false, embed);
            }
        }

        [Command("botinfo")]
        [Summary("Display info about this bot")]
        public async Task BotInfo()
        {
            var builder = new EmbedBuilder()
                .WithThumbnailUrl($"{Context.Client.CurrentUser.GetAvatarUrl()}")
                .WithColor(new Color(0, 0, 0))
                .AddField("Name:", $"{Context.Client.CurrentUser.Username}")
                .AddField("Developer:", "BlackLung#6950")
                .AddField("Bot created on:", $"{Context.Client.CurrentUser.CreatedAt.ToString("yyyy/MM/dd")}", true)
                .AddField("Written with:", "C# Discord.NET 3.0.2", true)
                .WithCurrentTimestamp();
            var embed = builder.Build();

            await Context.Channel.SendMessageAsync(null, false, embed);
        }

        [Command("Invite")]
        [Summary("Information on how to add this bot to your own server")]
        public async Task Invite()
        {
            var builder = new EmbedBuilder()
                .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                .WithColor(new Color(114, 137, 218))
                .WithTitle($"Add {Context.Client.CurrentUser.Username} To Your Server")
                .WithDescription(
                    $"**Invite {Context.Client.CurrentUser.Username} to your server by clicking the following link https://discord.com/api/oauth2/authorize?client_id=808888674900508723&permissions=8&scope=bot \nJoin the Official {Context.Client.CurrentUser.Username} discord server here https://discord.gg/WpEMeycgqa**")
                .WithFooter("Bot developed by: BlackLung#6950")
                .WithCurrentTimestamp();
            var embed = builder.Build();

            await Context.Channel.SendMessageAsync(null, false, embed);
        }

        [Command("help", RunMode = RunMode.Async)]
        [Summary("Displays a list of commands")]
        public async Task Help()
        {
            var guildPrefix = await _servers.GetGuildPrefix(Context.Guild.Id) ?? "!";

            var builder = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Description = "These are the commands you can use"
            };

            foreach (var module in _service.Modules)
            {
                string description = null;
                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context);
                    if (result.IsSuccess)

                        description += $"**{guildPrefix}{cmd.Aliases.First()}**\n*{cmd.Summary}*\n";
                }

                if (!string.IsNullOrWhiteSpace(description))
                    builder.AddField(x =>
                    {
                        x.Name = $"__**{module.Name}**__";
                        x.Value = description;
                        x.IsInline = false;
                    });
            }

            await ReplyAsync("", false, builder.Build());
        }


        [Command("server", RunMode = RunMode.Async)]
        [Summary("View information about the server")]
        public async Task Server()
        {
            var builder = new EmbedBuilder()
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithDescription("Server Information")
                .WithTitle($"{Context.Guild.Name} Server Stats")
                .WithColor(new Color(33, 176, 252))
                .AddField("Created At", Context.Guild.CreatedAt.ToString("yyyy/MM/dd"), true)
                .AddField("Member Count", Context.Guild.MemberCount + " members", true)
                .AddField("Online Users",
                    Context.Guild.Users.Where(x => x.Status != UserStatus.Offline).Count() + " members", true);
            var embed = builder.Build();

            await Context.Channel.SendMessageAsync(null, false, embed);
        }


        [Command("say", RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Make the bot say something\n(Admin permissions required)")]
        public async Task Say([Remainder] string msg)
        {
            await ReplyAsync(msg);
            await Context.Message.DeleteAsync();
        }

        [Command("rank", RunMode = RunMode.Async)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [Summary("Add a rank/role to yourself")]
        public async Task Rank([Remainder] string identifier)
        {
            await Context.Channel.TriggerTypingAsync();
            var ranks = await _ranksHelper.GetRanksAsync(Context.Guild);

            IRole role;

            if (ulong.TryParse(identifier, out var roleId))
            {
                var roleById = Context.Guild.Roles.FirstOrDefault(x => x.Id == roleId);
                if (roleById == null)
                {
                    await ReplyAsync("That role does not exist!");
                    return;
                }

                role = roleById;
            }
            else
            {
                var roleByName = Context.Guild.Roles.FirstOrDefault(x =>
                    string.Equals(x.Name, identifier, StringComparison.CurrentCultureIgnoreCase));
                if (roleByName == null)
                {
                    await ReplyAsync("That role does not exist!");
                    return;
                }

                role = roleByName;
            }

            if ((Context.User as SocketGuildUser).Roles.Any(x => x.Id == role.Id))
            {
                await (Context.User as SocketGuildUser).RemoveRoleAsync(role);
                await ReplyAsync($"Succesfully removed the rank {role.Mention} from you.");
                return;
            }

            await (Context.User as SocketGuildUser).AddRoleAsync(role);
            await ReplyAsync($"Succesfully added the rank {role.Mention} to you.");
        }
    }
}