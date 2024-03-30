﻿using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Janitor.Core.Model;
using Newtonsoft.Json;
using System.Data;
using ResponseMessageType = Janitor.Model.ResponseMessageType;

namespace Janitor.Handler
{
    public partial class HandleEvents : InteractionModuleBase
    {
        DiscordSocketClient _client;

        const string roleFriend = "Friend";
        const string addRoleCmd = $"Add {roleFriend} Role";
        const string removeRoleCmd = $"Remove {roleFriend} Role";
        const string modChannelName = "mod-log";

        //A simple list of some Janitor related sayings
        List<string> status = new List<string>()
        {
            "I'm a Janitor. What is your superpower?",
            "I never asked to be the world's best Janitor, but here I am, absolutely killing it.",
            "Never trust a Janitor with tattoos!",
            "Powered by Coffee.",
            "No one pays attention to the Janitor.",
            "Everything will be fine, the Janitor is here.",
            "↑ This is what a really cool Janitor looks like.",
            "What?", // Insider. ;)
            "Why are you looking at me like that?",
            "Sometimes I think I'm Batman.",
            "Anybody seen my broom?"
        };

        public HandleEvents(DiscordSocketClient client)
        {
            //Register new Events
            client.Ready += Client_Ready;

            client.JoinedGuild += Client_JoinedGuild;
            client.UserCommandExecuted += Client_UserCommandExecuted;
            client.ButtonExecuted += Client_ButtonExecuted;

            _client = client;
        }

        private async Task Client_ButtonExecuted(SocketMessageComponent arg)
        {
            var id = (ulong)arg.GuildId;
            var guild = _client.GetGuild(id);

            var uid = arg.Data.CustomId.ToString().Split('_')[1];
            var target = guild.GetUser(Convert.ToUInt64(uid));
            var user = guild.GetUser(arg.User.Id);
            var role = await GetOrCreateRole(guild, roleFriend);

            if (target.Roles.Contains(role))
            {
                await target.RemoveRoleAsync(guild.Roles.Where(x => x.Name == roleFriend).FirstOrDefault());

                var emb = new EmbedBuilder()
                {
                    Description = $"\"{roleFriend}\" Role has been removed from {target.Mention} by {user.Mention}.",
                    Color = Color.Red,
                };

                await arg.Channel.SendMessageAsync(embed: emb.Build());

                LogMessage(user, target, ResponseMessageType.FriendRoleRemoved.ToString(), removeRoleCmd, InformationType.Success);
            }
            await arg.DeferAsync();
        }

        private async Task Client_JoinedGuild(SocketGuild arg)
        {
            //If Client is ready Create Management role if it does not exist
            await GetOrCreateRole(arg, "Role Manager");
            AddUserCommand(arg);
        }

        private async Task Client_UserCommandExecuted(SocketUserCommand arg)
        {
            var command = arg.CommandName;
            var target = arg.Data.Member as SocketGuildUser;
            var user = arg.User as SocketGuildUser;
            var guild = _client.GetGuild((ulong)arg.GuildId);

            var roleManager = guild.Roles.Where(x => x.Name == "Role Manager").First();
            var roleJanitor = guild.Roles.Where(x => x.Name == "Janitor" && !x.IsManaged).First();

            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {guild.Name}: {user.DisplayName} invoked \"{command}\" for {target.DisplayName}");

            if (user == target)
                await SendInfo(arg, ResponseMessageType.CantEditYourself, target, user);
            else if (command == addRoleCmd)
            {
                if (user.Roles.Contains(roleManager) || user.Roles.Contains(roleJanitor))
                {
                    if (target.Roles.Where(x => x.Name == roleFriend).Count() == 1)
                        await SendInfo(arg, ResponseMessageType.UserHasRoleAlready, target, user);
                    else if (target.IsBot)
                        await SendInfo(arg, ResponseMessageType.BotCantHaveRole, target, user);
                    else if (target.Roles.Contains(roleJanitor))
                        await SendInfo(arg, ResponseMessageType.JanitorCantHaveRole, target, user);
                    else
                    {
                        await target.AddRoleAsync(guild.Roles.Where(x => x.Name == roleFriend).First());
                        await SendInfo(arg, ResponseMessageType.UserHasRoleNow, target, user);
                    }
                }
                else
                    await SendInfo(arg, ResponseMessageType.NotAllowed, target, user);
            }
            else if (command == removeRoleCmd)
            {
                if (user.Roles.Contains(roleManager))
                {
                    if (target.Roles.Where(x => x.Name == roleFriend).Count() == 0)
                        await SendInfo(arg, ResponseMessageType.UserDoesntHaveRole, target, user);
                    else
                        await SendInfo(arg, ResponseMessageType.RemoveFriendRole, target, user, new ComponentBuilder().WithButton($"Remove \"{roleFriend}\" Role", $"rf_{target.Id}", ButtonStyle.Danger).Build());
                }
                else
                    await SendInfo(arg, ResponseMessageType.NotAllowed, target, user);
            }
        }

        private async Task SendInfo(SocketUserCommand msg, ResponseMessageType type, SocketGuildUser target , SocketGuildUser user , MessageComponent component = null)
        {
            string text = string.Empty;
            Color col = Color.Red;
            InformationType result = InformationType.Error;

            switch (type)
            {
                case ResponseMessageType.NotAllowed:
                    text = "You are not allowed to do that!";
                    break;
                case ResponseMessageType.BotCantHaveRole:
                    if (target.DisplayName == "Janitor")
                        text = $"As much as I love you, I can't be your friend. :(";
                    else
                        text = $"A bot can't have the Role \"{roleFriend}\"!";
                    break;
                case ResponseMessageType.UserDoesntHaveRole:
                    text = $"({target.DisplayName}) doesn't have the Role \"{roleFriend}\"!";
                    break;
                case ResponseMessageType.UserHasRoleNow:
                    text = $"\"{roleFriend}\" Role has been granted to {target.Mention} by {user.Mention}.";
                    col = Color.Green;
                    result = InformationType.Success;
                    break;
                case ResponseMessageType.RemoveFriendRole:
                    text = $"({target.DisplayName}) will lose all access to private sections!\r\nDo you REALLY wish to remove the \"{roleFriend}\" Role?";
                    result = InformationType.Information;
                    break;
                case ResponseMessageType.JanitorCantHaveRole:
                    text = $"A Janitor can't have the Role \"{roleFriend}\"! They're cool enough already!";
                    break;
                case ResponseMessageType.UserHasRoleAlready:
                    text = $"({target.DisplayName}) already got the Role \"{roleFriend}\"!";
                    break;
                case ResponseMessageType.CantEditYourself:
                    text = $"You seem to have emotional problems. Try to join voice, we might be able to help you. :melting_face:";
                    col = Color.Blue;
                    break;
            }

            await msg.RespondAsync(embed: new EmbedBuilder()
            {
                Description = text,
                Color = col,
            }.Build(),
            components: component,
            ephemeral: type == ResponseMessageType.UserHasRoleNow ? false : true);

            LogMessage(user, target, type.ToString(), msg.CommandName, result);
        }

        private async void LogMessage(SocketGuildUser fromUser, SocketGuildUser targetUser, string message, string cmd, InformationType result = InformationType.Information)
        {
            SocketTextChannel channel = (SocketTextChannel)targetUser.Guild.Channels.Where(x => x.Name == modChannelName).FirstOrDefault();
            Color col = Color.Red;

            if (result == InformationType.Success)
                col = Color.Green;
            else if (result == InformationType.Information)
                col = Color.Blue;

            var emb = new EmbedBuilder()
            {
                Description = $"{fromUser.Mention} invoked \"{cmd}\" for {targetUser.Mention}\r\n-> {result}: {message}",
                //Timestamp = DateTime.Now,
                Color = col,
            };

            Console.WriteLine($"-> {result}: {message}");
            await channel.SendMessageAsync(embed: emb.Build());
        }

        private async Task Client_Ready()
        {
            var guilds = _client.Guilds;

            foreach (var guild in guilds)
            {
                await GetOrCreateRole(guild, "Role Manager");
                await GetOrCreateRole(guild, "Janitor");

                AddUserCommand(guild);
            }

            SetStatus();
        }

        private async Task<IRole> GetOrCreateRole(SocketGuild guild, string role = "")
        {
            IRole resRole = null;

            var res = guild.Roles.Where(x => x.Name == role).ToList();

            if (res.Count() == 0)
                resRole = await guild.CreateRoleAsync(role);
            else if (res.Count() == 1)
                resRole = res[0];

            return resRole;
        }

        private async void AddUserCommand(SocketGuild guild)
        {
            var guildUserCommandAddRole = new UserCommandBuilder();
            var guildUserCommandRemoveRole = new UserCommandBuilder();

            guildUserCommandAddRole.WithName(addRoleCmd);
            guildUserCommandRemoveRole.WithName(removeRoleCmd);

            try
            {
                await guild.BulkOverwriteApplicationCommandAsync(new ApplicationCommandProperties[]
                {
                    guildUserCommandAddRole.Build(),
                    guildUserCommandRemoveRole.Build(),
                });

            }
            catch (ApplicationCommandException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }
        }

        private async void SetStatus()
        {
            var t = new Thread(x =>
            {
                while (true)
                {
                    var BotStatus = status[new Random().Next(status.Count)];
                    _client.SetCustomStatusAsync(BotStatus);
                    Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Set bot status to \"{BotStatus}\".");
                    Thread.Sleep(TimeSpan.FromHours(1));
                }
            });
            t.Start();
        }
    }
}
