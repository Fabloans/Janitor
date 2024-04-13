using Discord;
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

        const string BotVersion = "1.0.1.7";
        const string roleFriend = "Friend";
        const string roleJanitor = "Janitor";
        const string roleManager = "Role Manager";
        const string addRoleCmd = $"Add {roleFriend} Role";
        const string removeRoleCmd = $"Remove {roleFriend} Role";
        const string modChannelName = "mod-log";

        // A simple list of some Janitor related sayings
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
            // Register new Events
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
            var friendRole = await GetOrCreateRole(guild, roleFriend);

            if (target.Roles.Contains(friendRole))
            {
                bool success = false;
                try
                {
                    await target.RemoveRoleAsync(guild.Roles.Where(x => x.Name == roleFriend).FirstOrDefault());
                    success = true;
                }
                catch
                {
                    await arg.RespondAsync(embed: new EmbedBuilder()
                    {
                        Description = $"ERROR: Janitor Bot is missing permission \"Manage Roles\"!",
                        Color = Color.Red,
                    }.Build(),
                    ephemeral: true);
                    
                    LogMessage(user.Guild.Name, $"{user.Mention} invoked \"{removeRoleCmd}\" for {target.Mention}", ResponseMessageType.MissingManagerPermission, InformationType.Error);
                }

                if (success)
                {
                    try // Try to send as message, fallback to response in case of missing permissions.
                    {
                        await arg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                        {
                            Description = $"\"{roleFriend}\" Role has been removed from {target.Mention} by {user.Mention}.",
                            Color = Color.Orange,
                        }.Build());
                        await arg.DeferAsync();
                    }
                    catch
                    {
                        await arg.RespondAsync(embed: new EmbedBuilder()
                        {
                            Description = $"\"{roleFriend}\" Role has been removed from {target.Mention} by {user.Mention}.",
                            Color = Color.Orange,
                        }.Build());
                    }

                    LogMessage(user.Guild.Name, $"{user.Mention} invoked \"{removeRoleCmd}\" for {target.Mention}", ResponseMessageType.FriendRoleRemoved, InformationType.Success);
                }            
            }
        }

        private async Task Client_JoinedGuild(SocketGuild arg)
        {
            // If Client is ready, create Roles if they don't not exist
            await GetOrCreateRole(arg, roleFriend);
            await GetOrCreateRole(arg, roleManager);
            AddUserCommand(arg);
        }

        private async Task Client_UserCommandExecuted(SocketUserCommand arg)
        {
            await arg.DeferAsync(ephemeral: true);

            var command = arg.CommandName;
            var target = arg.Data.Member as SocketGuildUser;
            var user = arg.User as SocketGuildUser;
            var guild = _client.GetGuild((ulong)arg.GuildId);

            var FriendRole = guild.Roles.Where(x => x.Name == roleFriend).FirstOrDefault();
            var JanitorRole = guild.Roles.Where(x => x.Name == roleJanitor && !x.IsManaged).FirstOrDefault();
            var ManagerRole = guild.Roles.Where(x => x.Name == roleManager).FirstOrDefault();

            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {guild.Name}: {user.DisplayName} invoked \"{command}\" for {target.DisplayName}");

            if (FriendRole == null || ManagerRole == null)
            {
                await SendInfo(arg, ResponseMessageType.MissingRoles, target, user);
            }
            else if (user == target)
                await SendInfo(arg, ResponseMessageType.CantEditYourself, target, user);
            else if (command == addRoleCmd)
            {
                if (user.Roles.Contains(ManagerRole) || user.Roles.Contains(JanitorRole))
                {
                    if (target.Roles.Where(x => x.Name == roleFriend).Count() == 1)
                        await SendInfo(arg, ResponseMessageType.UserHasRoleAlready, target, user);
                    else if (target.IsBot)
                        await SendInfo(arg, ResponseMessageType.BotCantHaveRole, target, user);
                    else if (target.Roles.Contains(JanitorRole))
                        await SendInfo(arg, ResponseMessageType.JanitorCantHaveRole, target, user);
                    else
                    {
                        bool success = false;
                        try
                        {                          
                            await target.AddRoleAsync(guild.Roles.Where(x => x.Name == roleFriend).FirstOrDefault());
                            success = true;
                        }
                        catch
                        {
                            await SendInfo(arg, ResponseMessageType.MissingManagerPermission, target, user);
                        }
                        
                        if (success)
                            await SendInfo(arg, ResponseMessageType.UserHasRoleNow, target, user);
                    }
                }
                else
                    await SendInfo(arg, ResponseMessageType.NotAllowed, target, user);
            }
            else if (command == removeRoleCmd)
            {
                if (user.Roles.Contains(ManagerRole))
                {
                    if (target.Roles.Where(x => x.Name == roleFriend).Count() == 0)
                        await SendInfo(arg, ResponseMessageType.UserDoesntHaveRole, target, user);
                    else
                        await SendInfo(arg, ResponseMessageType.RemoveFriendRole, target, user);
                }
                else
                    await SendInfo(arg, ResponseMessageType.NotAllowed, target, user);
            }
        }

        private async Task SendInfo(SocketUserCommand msg, ResponseMessageType type, SocketGuildUser target , SocketGuildUser user)
        {
            string text = string.Empty;
            Color col = Color.Red;
            InformationType result = InformationType.Information;
            MessageComponent component = null;

            switch (type)
            {
                case ResponseMessageType.BotCantHaveRole:
                    if (target.DisplayName == _client.CurrentUser.Username)
                        text = $"As much as I love you, I can't be your friend. :(";
                    else
                        text = $"A bot can't have the Role \"{roleFriend}\"!";
                    break;
                case ResponseMessageType.CantEditYourself:
                    text = $"You seem to have emotional problems. Try to join voice, we might be able to help you. :melting_face:";
                    col = Color.Blue;
                    break;
                case ResponseMessageType.JanitorCantHaveRole:
                    text = $"A {roleJanitor} can't have the Role \"{roleFriend}\"! They're cool enough already!";
                    break;
                case ResponseMessageType.MissingManagerPermission:
                    text = $"ERROR: Janitor Bot is missing permission \"Manage Roles\"!";
                    result = InformationType.Error;
                    break;
                case ResponseMessageType.MissingRoles:
                    text = $"ERROR: Either the \"{roleFriend}\" or the \"{roleManager}\" Role is missing!";
                    result = InformationType.Error;
                    break;
                case ResponseMessageType.NotAllowed:
                    text = "You are not allowed to do that!";
                    result = InformationType.Alert;
                    break;
                case ResponseMessageType.RemoveFriendRole:
                    text = $"{target.Mention} will lose all access to private sections!\r\nDo you REALLY wish to remove the \"{roleFriend}\" Role?";
                    component = new ComponentBuilder().WithButton($"Remove \"{roleFriend}\" Role", $"rf_{target.Id}", ButtonStyle.Danger).Build();
                    result = InformationType.Alert;
                    break;
                case ResponseMessageType.UserDoesntHaveRole:
                    text = $"{target.Mention} doesn't have the Role \"{roleFriend}\"!";
                    break;
                case ResponseMessageType.UserHasRoleAlready:
                    text = $"{target.Mention} already got the Role \"{roleFriend}\"!";
                    break;
                case ResponseMessageType.UserHasRoleNow:
                    text = $"\"{roleFriend}\" Role has been granted to {target.Mention} by {user.Mention}.";
                    result = InformationType.Success;
                    break;                
            }

            switch (result)
            {
                case InformationType.Alert:
                    col = Color.Orange;
                    break;
                case InformationType.Information:
                    col = Color.Blue;
                    break;
                case InformationType.Success:
                    col = Color.Green;
                    break;
            }

            if (result == InformationType.Information)
                col = Color.Blue;
            else if (result == InformationType.Success)
                col = Color.Green;


            if (type == ResponseMessageType.UserHasRoleNow) {
                try // Try to send as message, fallback to ephemeral response in case of missing permissions.
                {
                    await msg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                    {
                        Description = text,
                        Color = col,
                    }.Build());
                    await msg.DeleteOriginalResponseAsync();
                }
                catch
                {
                    await msg.FollowupAsync(embed: new EmbedBuilder()
                    {
                        Description = text,
                        Color = col,
                    }.Build());
                }
            }
            else
            {
                await msg.FollowupAsync(embed: new EmbedBuilder()
                {
                    Description = text,
                    Color = col,
                }.Build(),
                components: component);
            }

            LogMessage(user.Guild.Name, $"{user.Mention} invoked \"{msg.CommandName}\" for {target.Mention}", type, result);
        }

        private async void LogMessage(string server, string message, ResponseMessageType type, InformationType result = InformationType.Information)
        {
            SocketTextChannel channel = (SocketTextChannel)_client.Guilds.Where(x => x.Name == server).FirstOrDefault().Channels.Where(x => x.Name == modChannelName).FirstOrDefault();

            Color col = Color.Red;

            switch (result)
            {
                case InformationType.Alert:
                    col = Color.Orange;
                    break;
                case InformationType.Information:
                    col = Color.Blue;
                    break;
                case InformationType.Success:
                    col = Color.Green;
                    break;
            }

            Console.WriteLine($"-> {result}: {type}");

            if (channel != null) // If channel doesn't exist or we don't have permission, just ignore.
                try
                {
                    await channel.SendMessageAsync(embed: new EmbedBuilder()
                    {
                        Description = $"{message}\r\n-> {result}: {type}",
                        Color = col,
                    }.Build());
                }
                catch
                {
                }
        }

        private async Task Client_Ready()
        {
            var guilds = _client.Guilds;

            foreach (var guild in guilds)
            {
                AddUserCommand(guild);

                Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {guild.Name}: Janitor Bot v{BotVersion}.");
                LogMessage(guild.Name, $"Janitor Bot v{BotVersion}.", ResponseMessageType.BotStarted, InformationType.Information);
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
            var StatusThread = new Thread(x =>
            {
                while (true)
                {
                    var BotStatus = status[new Random().Next(status.Count)];
                    _client.SetCustomStatusAsync(BotStatus);
                    Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Set bot status to \"{BotStatus}\".");
                    Thread.Sleep(TimeSpan.FromHours(1));
                }
            });
            StatusThread.Start();
        }
    }
}