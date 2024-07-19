using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Janitor.Core.Model;
using Newtonsoft.Json;
using System.Data;

namespace Janitor.Handler
{
    public partial class HandleEvents : InteractionModuleBase
    {
        DiscordSocketClient _client;

        const string BotVersion = "1.0.2.6";
        const string roleFriend = "Friend";
        const string roleGuest = "Guest";
        const string roleJanitor = "Janitor";
        const string roleManager = "Role Manager";
        const string addFriendRoleCmd = $"Add \"{roleFriend}\" Role";
        const string removeFriendRoleCmd = $"Remove \"{roleFriend}\" Role";
        const string announceChannelName = "guest-lounge"; // Automatic roleGuest assignments go here.
        const string modChannelName = "mod-log"; // Bot logging channel.

        // A simple list of some Janitor related sayings
        List<string> status = new List<string>()
        {
            "I'm a Janitor. What is your superpower?",
            "I never asked to be the world's best Janitor, but here I am, absolutely killing it.",
            "Never trust a Janitor without tattoos!",
            "Powered by beer.", // ;)
            "No one pays attention to the Janitor.",
            "Everything will be fine, the Janitor is here.",
            "↑ This is what a really cool Janitor looks like.",
            "What?", // Insider. ;)
            "Why are you looking at me like that?",
            "Sometimes I think I'm Batman.",
            "Anybody seen my broom?",
            "Why are you looking at me like that?",
            "Exterminate!",
            "Peace is my profession, mass murder is just a hobby." // The PJI motto. ;)
        };

        public HandleEvents(DiscordSocketClient client)
        {
            // Register new Events
            client.Ready += Client_Ready;
            client.JoinedGuild += Client_JoinedGuild;
            client.UserJoined += Client_UserJoined;
            client.UserCommandExecuted += Client_UserCommandExecuted;
            client.ButtonExecuted += Client_RemoveRoleButtonExecuted;

            _client = client;
        }

        private async Task Client_JoinedGuild(SocketGuild guild)
        {
            LogMessage(guild.Id, $"Janitor Bot v{BotVersion} joined.", InformationType.Information, ResponseMessageType.BotJoined);

            // Create essential roles when joining guild.
            await GetOrCreateRole(guild, roleFriend);
            await GetOrCreateRole(guild, roleManager);

            AddUserCommand(guild);
        }

        private async Task Client_Ready()
        {
            var guilds = _client.Guilds;

            foreach (var guild in guilds)
            {
                Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {guild.Name}: Janitor Bot v{BotVersion} ready.");
#if DEBUG
                LogMessage(guild.Id, $"Janitor Bot v{BotVersion} ready.", InformationType.Information, ResponseMessageType.BotReady);
#endif

                // Create essential roles when client is ready.
                await GetOrCreateRole(guild, roleFriend);
                await GetOrCreateRole(guild, roleManager);

                AddUserCommand(guild);
            }

            SetStatus();
        }

        private async Task Client_UserJoined(SocketGuildUser user)
        {
            var guild = _client.GetGuild(user.Guild.Id);
            var GuestRole = guild.Roles.Where(x => x.Name == roleGuest).FirstOrDefault();
            var channel = guild.Channels.FirstOrDefault(x => x.Name == announceChannelName) as SocketTextChannel;

            if (GuestRole != null && !user.IsBot)
            {
                Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {guild.Name}: {user.DisplayName} joined. Assigning \"{GuestRole}\" to {user.DisplayName}");

                try
                {
                    await user.AddRoleAsync(GuestRole);
                    await SendMessageToChannel(channel, $"Temporary \"{roleGuest}\" role has been granted to {user.Mention}.", Color.Orange);
                    LogMessage(guild.Id, $"User joined. \"{GuestRole}\" role granted to {user.Mention}.", InformationType.Success, ResponseMessageType.AddGuestRole);
                }
                catch
                {
                    LogMessage(guild.Id, $": \"{GuestRole}\".", InformationType.ERROR, ResponseMessageType.MissingManagerPermission);
                }
            }
        }

        private async Task GetOrCreateRole(SocketGuild guild, string role)
        {
            var createRole = guild.Roles.FirstOrDefault(x => x.Name == role) as IRole;

            if (createRole == null)
            {
                Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {guild.Name}: Create missing role: \"{role}\".");

                try
                {
                    createRole = await guild.CreateRoleAsync(role);
                    LogMessage(guild.Id, $"Creating missing role: \"{role}\".", InformationType.Success, ResponseMessageType.CreateRole);
                }
                catch
                {
                    LogMessage(guild.Id, $"Creating missing role: \"{role}\".", InformationType.ERROR, ResponseMessageType.MissingManagerPermission);
                }
            }
        }

        private async void AddUserCommand(SocketGuild guild)
        {
            var guildUserCommandAddRole = new UserCommandBuilder();
            var guildUserCommandRemoveRole = new UserCommandBuilder();

            guildUserCommandAddRole.WithName(addFriendRoleCmd);
            guildUserCommandRemoveRole.WithName(removeFriendRoleCmd);

            try
            {
                await guild.BulkOverwriteApplicationCommandAsync(new ApplicationCommandProperties[]
                {
                    guildUserCommandAddRole.Build(),
                    guildUserCommandRemoveRole.Build(),
                });

            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }
        }

        private async Task<bool> SendMessageToChannel(SocketTextChannel channel, string text, Color col)
        {
            if (channel != null) // If channel doesn't exist or we don't have permission, just ignore.
            {
                try
                {
                    await channel.SendMessageAsync(embed: new EmbedBuilder()
                    {
                        Description = text,
                        Color = col,
                    }.Build());
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        private async Task SendMessageFollowUp(SocketUserCommand cmd, string text, Color col, MessageComponent component = null)
        {
            await cmd.FollowupAsync(embed: new EmbedBuilder()
            {
                Description = text,
                Color = col,
            }.Build(),
            components: component);
        }

        private async Task SendMessageModify(SocketMessageComponent msg, string text, Color col)
        {
            await msg.ModifyOriginalResponseAsync(msg => msg.Embed = new EmbedBuilder()
            {
                Description = text,
                Color = col,
            }.Build());
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

        private async Task Client_UserCommandExecuted(SocketUserCommand cmd)
        {
            await cmd.DeferAsync(ephemeral: true);

            var command = cmd.CommandName;
            var target = cmd.Data.Member as SocketGuildUser;
            var user = cmd.User as SocketGuildUser;
            var guild = _client.GetGuild((ulong)cmd.GuildId);

            var FriendRole = guild.Roles.FirstOrDefault(x => x.Name == roleFriend);
            var GuestRole = guild.Roles.FirstOrDefault(x => x.Name == roleGuest);
            var JanitorRole = guild.Roles.FirstOrDefault(x => x.Name == roleJanitor && !x.IsManaged);
            var ManagerRole = guild.Roles.FirstOrDefault(x => x.Name == roleManager);

            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {guild.Name}: {user.DisplayName} invoked \"{command}\" for {target.DisplayName}");

            if (FriendRole == null || (ManagerRole == null && JanitorRole == null))
            {
                await SendInfo(cmd, ResponseMessageType.MissingRoles, target, user);
            }
            else if (user == target)
                await SendInfo(cmd, ResponseMessageType.CantEditYourself, target, user);
            else if (command == addFriendRoleCmd)
            {
                if (user.Roles.Contains(ManagerRole) || user.Roles.Contains(JanitorRole))
                {
                    if (target.Roles.Contains(FriendRole))
                        await SendInfo(cmd, ResponseMessageType.UserHasFriendRoleAlready, target, user);
                    else if (target.IsBot)
                        await SendInfo(cmd, ResponseMessageType.BotCantHaveRole, target, user);
                    else if (target.Roles.Contains(JanitorRole))
                        await SendInfo(cmd, ResponseMessageType.JanitorCantHaveRole, target, user);
                    else
                    {
                        try
                        {
                            await target.AddRoleAsync(FriendRole);
                            if (target.Roles.Contains(GuestRole))
                            {
                                await target.RemoveRoleAsync(GuestRole);
                                await SendInfo(cmd, ResponseMessageType.AddFriendRole, target, user, GuestRole);
                            }
                            else   
                                await SendInfo(cmd, ResponseMessageType.AddFriendRole, target, user);
                        }
                        catch
                        {
                            await SendInfo(cmd, ResponseMessageType.MissingManagerPermission, target, user);
                        }
                    }
                }
                else
                    await SendInfo(cmd, ResponseMessageType.MissingUserPermission, target, user);
            }
            else if (command == removeFriendRoleCmd)
            {
                if (user.Roles.Contains(ManagerRole))
                {
                    if (target.Roles.Contains(FriendRole))
                        await SendInfo(cmd, ResponseMessageType.RemoveFriendRole, target, user);
                    else
                        await SendInfo(cmd, ResponseMessageType.UserDoesntHaveFriendRole, target, user);
                }
                else
                    await SendInfo(cmd, ResponseMessageType.MissingUserPermission, target, user);
            }
        }

        private async Task SendInfo(SocketUserCommand cmd, ResponseMessageType type, SocketGuildUser target , SocketGuildUser user, SocketRole GuestRole = null)
        {
            var text = string.Empty;
            var col = Color.Red;
            var result = InformationType.Information;
            MessageComponent component = null;

            switch (type)
            {
                case ResponseMessageType.AddFriendRole:
                    text = $"\"{roleFriend}\" role has been granted to {target.Mention} by {user.Mention}.";
                    if (GuestRole != null)
                        text += $"\r\n\"{roleGuest}\" role has been removed.";
                    result = InformationType.Success;
                    break;
                case ResponseMessageType.BotCantHaveRole:

                    if (target.Id == _client.CurrentUser.Id)
                        text = $"As much as I love you, I can't be your friend. :cry:";
                    else
                        text = $"A bot can't have the \"{roleFriend}\" role!";
                    break;
                case ResponseMessageType.CantEditYourself:
                    text = $"You seem to have emotional problems. Try to join voice, they might be able to help you. :melting_face:";
                    break;
                case ResponseMessageType.JanitorCantHaveRole:
                    text = $"A {roleJanitor} can't have the \"{roleFriend}\" role! They're cool enough already!";
                    break;
                case ResponseMessageType.MissingManagerPermission:
                    text = $"ERROR: Janitor Bot is missing \"Manage Roles\" permission!";
                    result = InformationType.ERROR;
                    break;
                case ResponseMessageType.MissingRoles:
                    text = $"ERROR: The \"{roleFriend}\" or either the \"{roleManager}\" or \"{roleJanitor}\" role is missing!";
                    result = InformationType.ERROR;
                    break;
                case ResponseMessageType.MissingUserPermission:
                    text = "You are not allowed to do that!";
                    result = InformationType.Alert;
                    break;
                case ResponseMessageType.RemoveFriendRole:
                    text = $"{target.Mention} will lose all access to private sections!\r\nDo you **REALLY** wish to remove the \"{roleFriend}\" role?";
                    component = new ComponentBuilder().WithButton($"{removeFriendRoleCmd}", $"{target.Id}", ButtonStyle.Danger).Build();
                    result = InformationType.Alert;
                    break;
                case ResponseMessageType.UserDoesntHaveFriendRole:
                    text = $"{target.Mention} doesn't have the \"{roleFriend}\" role!";
                    break;
                case ResponseMessageType.UserHasFriendRoleAlready:
                    text = $"{target.Mention} already has the \"{roleFriend}\" role!";
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

            if (type == ResponseMessageType.AddFriendRole) {
                // Try to send as message, fallback to ephemeral response in case of missing permissions.
                if (await SendMessageToChannel((SocketTextChannel)cmd.Channel, text, col))
                    await cmd.DeleteOriginalResponseAsync();
                else
                    await SendMessageFollowUp(cmd, text, col);
            }
            else
                await SendMessageFollowUp(cmd, text, col, component);

            if (type == ResponseMessageType.AddFriendRole && GuestRole != null)
                LogMessage(user.Guild.Id, $"{user.Mention} invoked \"{cmd.CommandName}\" for {target.Mention}", result, type, ResponseMessageType.RemoveGuestRole);
            else
                LogMessage(user.Guild.Id, $"{user.Mention} invoked \"{cmd.CommandName}\" for {target.Mention}", result, type);
        }

        private async Task Client_RemoveRoleButtonExecuted(SocketMessageComponent msg)
        {
            await msg.DeferAsync(); // Needed for .Modify[...]() and .Delete[...]() to work.
            await msg.ModifyOriginalResponseAsync(msg => msg.Components = new ComponentBuilder().Build()); // Remove Button on click.

            var guild = _client.GetGuild((ulong)msg.GuildId);
            var target = guild.GetUser(Convert.ToUInt64(msg.Data.CustomId));
            var user = guild.GetUser(msg.User.Id);
            var FriendRole = guild.Roles.FirstOrDefault(x => x.Name == roleFriend);
            var GuestRole = guild.Roles.FirstOrDefault(x => x.Name == roleGuest);

            if (target.Roles.Contains(FriendRole))
            {
                bool success = false;
                try
                {
                    await target.RemoveRoleAsync(FriendRole);
                    if (GuestRole != null)
                        await target.AddRoleAsync(GuestRole);
                    success = true;
                }
                catch
                {
                    await SendMessageModify(msg, $"ERROR: Janitor Bot is missing \"Manage Roles\" permission!", Color.Red);
                    LogMessage(user.Guild.Id, $"{user.Mention} invoked \"{removeFriendRoleCmd}\" for {target.Mention}", InformationType.ERROR, ResponseMessageType.MissingManagerPermission);
                }

                if (success)
                {
                    string text = $"\"{roleFriend}\" role has been removed from {target.Mention} by {user.Mention}.";

                    if (GuestRole != null)
                        text += $"\r\n\"{roleGuest}\" role has been granted.";

                    // Try to send as message, fallback to ephemeral response in case of missing permissions.
                    if (await SendMessageToChannel((SocketTextChannel)msg.Channel, text, Color.Orange))
                        await msg.DeleteOriginalResponseAsync();
                    else
                        await SendMessageModify(msg, text, Color.Orange);

                    if (GuestRole != null)
                        LogMessage(user.Guild.Id, $"{user.Mention} invoked \"{removeFriendRoleCmd}\" for {target.Mention}", InformationType.Success, ResponseMessageType.RemoveFriendRole, ResponseMessageType.AddGuestRole);
                    else
                        LogMessage(user.Guild.Id, $"{user.Mention} invoked \"{removeFriendRoleCmd}\" for {target.Mention}", InformationType.Success, ResponseMessageType.RemoveFriendRole);
                }
            }
        }

        private async void LogMessage(ulong server, string message, InformationType result, ResponseMessageType type, ResponseMessageType type2 = ResponseMessageType.Null)
        {
            var channel = _client.GetGuild(server).Channels.FirstOrDefault(x => x.Name == modChannelName) as SocketTextChannel;
            var col = Color.Red;

            Console.WriteLine($"-> {result}: {type}");
            if (type2 != ResponseMessageType.Null)
                Console.WriteLine($"-> {result}: {type2}");

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

            string text = $"{message}\r\n-> {result}: {type}";
            if (type2 != ResponseMessageType.Null)
                text += $"\r\n -> {result}: {type2}";

            await SendMessageToChannel(channel, text, col);
        }
    }
}