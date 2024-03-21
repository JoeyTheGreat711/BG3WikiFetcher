//invite: https://discord.com/oauth2/authorize?client_id=1220175919117893723&permissions=3072&scope=bot

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace BG3WikiFetcher
{
    /// <summary>
    /// class containing functions to interface with discord
    /// </summary>
    public class DiscordHandler
    {
        //static client object
        private static DiscordSocketClient discordClient;
        /// <summary>
        /// login to discord and start bot
        /// </summary>
        /// <param name="secrets">object containing secrets</param>
        public static async Task initialize(Secrets secrets)
        {
            //connect to discord and start bot
            DiscordSocketConfig config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            discordClient = new DiscordSocketClient(config);
            discordClient.Log += Log;
            discordClient.MessageReceived += MessageRecieved;
            await discordClient.LoginAsync(TokenType.Bot, secrets.discordToken);
            await discordClient.StartAsync();
        }
        /// <summary>
        /// discord logmessage handler, prints logs to console with discord flag
        /// </summary>
        /// <param name="msg">log message</param>
        private static Task Log(LogMessage msg)
        {
            Console.WriteLine("[Discord] " + msg.ToString());
            return Task.CompletedTask;
        }
        /// <summary>
        /// handler for Discord's messageRecieved event
        /// </summary>
        /// <param name="messageData">object containing info about the message</param>
        /// <returns></returns>
        private static async Task MessageRecieved(SocketMessage messageData)
        {
            if (messageData.Author.IsBot) return;
            string? reply = DiscordReply(messageData.Content);
            if (reply == null) return;
            MessageReference reference = new MessageReference(messageId: messageData.Id, channelId: messageData.Channel.Id);
            await messageData.Channel.SendMessageAsync(text: reply, messageReference: reference);
        }
        /// <summary>
        /// generates formatted string with links to mentioned pages
        /// </summary>
        /// <param name="messageBody">entire message body</param>
        /// <returns>formatted string to reply with</returns>
        private static string? DiscordReply(string messageBody)
        {
            List<Page> pages = Wiki.findPages(messageBody);
            if (pages.Count == 0) return null;
            string reply = "";
            foreach (Page p in pages)
                reply += string.Format("[{0}](<{1}>)\n", p.title, p.getUrl());
            return reply;
        }
    }
}
