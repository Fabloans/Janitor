using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Data;
using MessageType = Janitor.Model.MessageType;

namespace Janitor.Handler
{
    public partial class HandleEvents : InteractionModuleBase
    {
        DiscordSocketClient _client;

        string FriendRole = "Friend";

        //A simple list of some Janitor related sayings
        List<string> status = new List<string>()
        {
            "I'm a Janitor. \r\nWhat is your superpower?",
            "I never asked to be the world's best Janitor, but here I am absolutely killing it.",
            "Never trust a janitor with tattoos",
            "Powered by Coffee",
            "No one pays attention to the janitor.",
            "Everything will be fine, the janitor is here.",
            "World's okayest Janitor",
            "↑ This is what a really cool janitor looks like"
        };

        public HandleEvents(DiscordSocketClient client)
        {
            //Register new Events
            client.Ready += Client_Ready;

            client.JoinedGuild += Client_JoinedGuild;
            client.UserCommandExecuted += Client_UserCommandExecuted;

            _client = client;
        }

        private async Task Client_JoinedGuild(SocketGuild arg)
        {
            //If Client is ready Create Management role if it does not exist
            await GetOrCreateRole(arg, "Role Manager");
            AddUserCommand(arg);
        }

        private async Task Client_UserCommandExecuted(SocketUserCommand arg)
        {
            var target = arg.Data.Member as SocketGuildUser;
            var user = arg.User as SocketGuildUser;
            var guild = _client.GetGuild((ulong)arg.GuildId);

            var roleManager = guild.Roles.Where(x => x.Name == "Role Manager").ToList()[0];
            var roleJanitor1 = guild.Roles.Where(x => x.Name == "Janitor").ToList();
            roleJanitor1 = roleJanitor1.Where(x => !x.IsManaged).ToList();
            roleJanitor1 = roleJanitor1.Where(x => x.Members.Where(x => x.Id == _client.CurrentUser.Id).Count() == 0).ToList();
            var roleJanitor = roleJanitor1.ToList()[0];

            if (user.Roles.Contains(roleManager))
            {
                if (user == target)
                {
                    await SendInfo(arg, MessageType.CantAddRoleToYourself, target);
                    return;
                }
                else if (target.Roles.Where(x => x.Name == FriendRole).Count() == 1)
                {
                    await SendInfo(arg, MessageType.UserHasRoleAllready, target);
                    return;
                }
                else if (target.IsBot)
                    await SendInfo(arg, MessageType.BotCantHaveRole);
                else if (target.Roles.Contains(roleJanitor))
                    await SendInfo(arg, MessageType.JanitorCantHaveRole, target, user);
                else
                {
                    await target.AddRoleAsync(guild.Roles.Where(x => x.Name == FriendRole).FirstOrDefault());
                    await SendInfo(arg, MessageType.UserHasRoleNow, target, user);
                }
            }
            else
                await SendInfo(arg, MessageType.NotAllowed);
        }

        private async Task SendInfo(SocketUserCommand msg, MessageType type, SocketGuildUser target = null, SocketGuildUser user = null)
        {
            string text = string.Empty;
            Color col = Color.Red;

            switch (type)
            {
                case MessageType.NotAllowed:
                    text = "You are not allowed to do that!";
                    col = Color.Red;
                    break;
                case MessageType.BotCantHaveRole:
                    text = $"A bot can't have the Role \"{FriendRole}\"!";
                    col = Color.Red;
                    break;
                case MessageType.UserHasRoleNow:
                    text = $"({target.DisplayName}) has now the Role \"{FriendRole}\"!";
                    col = Color.Green;
                    break;
                case MessageType.JanitorCantHaveRole:
                    text = $"A Janitor can't have the Role \"{FriendRole}\"!";
                    col = Color.Red;
                    break;
                case MessageType.UserHasRoleAllready:
                    text = $"({target.DisplayName}) already got the Role \"{FriendRole}\"!";
                    col = Color.Blue;
                    break;
                case MessageType.CantAddRoleToYourself:
                    text = $"You are not supposed to do that... Nice Try tho. :melting_face:";
                    col = Color.Blue;
                    break;
            }

            await msg.RespondAsync(embed: new EmbedBuilder()
            {
                Title = text,
                Color = col,
            }.Build(),
            ephemeral: true);

            if (type == MessageType.UserHasRoleNow)
                await msg.Channel.SendMessageAsync($"\"{FriendRole}\" role has been granted to {target.Mention} by {user.Mention}.");
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
            var guildUserCommand = new UserCommandBuilder();
            var guildMessageCommand = new MessageCommandBuilder();

            guildUserCommand.WithName($"Add {FriendRole} Role");
            guildMessageCommand.WithName($"Add {FriendRole} Role");

            try
            {
                await guild.BulkOverwriteApplicationCommandAsync(new ApplicationCommandProperties[]
                {
                    guildUserCommand.Build(),
                    guildMessageCommand.Build(),
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
                    _client.SetCustomStatusAsync(status[new Random().Next(status.Count)]);
                    var delay = TimeSpan.FromHours(new Random().Next(1, 24));
                    Thread.Sleep(delay);
                }
            });
            t.Start();
        }
    }
}
