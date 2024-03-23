using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Xml.Linq;

namespace Janitor.Handler
{
    public class HandleEvents : InteractionModuleBase
    {
        DiscordSocketClient _client;

        IRole _role;
        string FriendRole = "Friend";

        List<string> status = new List<string>()
        {
            "I'm a Janitor. \r\nWhat is your superpower?",
            "I never asked to be the world's best Janitor, but here I am absolutely killing it.",
            "Never trust a janitor with tattoos",
            "Powered by Coffee",
            "No one plays attention to the janitor.",
            "Everything will be fine, the janitor is here.",
            "World's okayest Janitor",
            "↑ This is what a really cool janitor looks like"
        };
        public HandleEvents(DiscordSocketClient client)
        {
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
            var name = target.GlobalName == string.Empty ? target.Username : target.GlobalName;

            var user = arg.User as SocketGuildUser;
            if (user.Roles.Contains(_role))
            {
                await target.RemoveRoleAsync(guild.Roles.Where(x => x.Name == FriendRole).FirstOrDefault());
                await arg.RespondAsync(embed: new EmbedBuilder()
                {
                    Title = $"({name}) has no longer the Role \"{FriendRole}\"!",
                    Color = Color.DarkRed
                }.Build(),
                ephemeral: true);
                await arg.Message.DeleteAsync();
            }
        }

        private async Task Client_UserCommandExecuted(SocketUserCommand arg)
        {
            var target = arg.Data.Member as SocketGuildUser;
            var user = arg.User as SocketGuildUser;
            var id = (ulong)arg.GuildId;
            var guild = _client.GetGuild(id);
            var name = target.GlobalName == string.Empty ? target.Username : target.GlobalName;

            if (user.Roles.Contains(_role))
            {
                if (target.IsBot)
                    await arg.RespondAsync(embed: new EmbedBuilder()
                    { 
                        Title = $"A bot can't have the Role \"{FriendRole}\"!", 
                        Color = Color.Red
                    }.Build(),
                    ephemeral: true);
                else
                {
                    if (target.Roles.Where(x => x.Name == FriendRole).Count() == 1)
                    {
                        var comp = new ComponentBuilder().WithButton($"Remove \"{FriendRole}\"", $"rf_{target.Id}", ButtonStyle.Danger);
                        await arg.RespondAsync(
                            embed: new EmbedBuilder()
                            { 
                                Title = $"({name}) has already the Role \"{FriendRole}\"!\r\nDo you wish to remove it?", 
                                Color = Color.Red,
                            }.Build(),
                            components: comp.Build(), 
                            ephemeral: true);
                    }
                    else
                    {
                        await target.AddRoleAsync(guild.Roles.Where(x => x.Name == FriendRole).FirstOrDefault());
                        await arg.RespondAsync(embed: new EmbedBuilder()
                        { 
                            Title = $"({name}) has now the Role \"{FriendRole}\"!",
                            Color = Color.Green 
                        }.Build(),
                            ephemeral: true);
                    }
                }
            }
            else
                await arg.RespondAsync(embed: new EmbedBuilder()
                { 
                    Title = "You are not allowed to do that!", 
                    Color = Color.Red
                }.Build(), 
                ephemeral: true);
        }
        private async Task Client_JoinedGuild(SocketGuild arg)
        {
            var role = arg.Roles.Where(x => x.Name == "RoleManager").ToList();
            if (role.Count() == 0)
                _role = await arg.CreateRoleAsync("RoleManager", color: Color.Purple);
            else if (role.Count() == 1)
                _role = role[0];
        }

        private async Task Client_Ready()
        {
            var guild = _client.GetGuild(792533822587535391);

            var role = guild.Roles.Where(x => x.Name == "RoleManager").ToList();
            if (role.Count() == 0)
                _role = await guild.CreateRoleAsync("RoleManager", color: Color.Purple);
            else if (role.Count() == 1)
                _role = role[0];

            SetStatus();


            var guildUserCommand = new UserCommandBuilder();
            var guildMessageCommand = new MessageCommandBuilder();

            guildUserCommand.WithName($"Add {FriendRole} Role");
            guildMessageCommand.WithName($"Add {FriendRole} Role");

            try
            {
                await guild.BulkOverwriteApplicationCommandAsync(new ApplicationCommandProperties[]
                {
                    guildUserCommand.Build(),
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
