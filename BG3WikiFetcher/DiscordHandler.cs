//invite: https://discord.com/oauth2/authorize?client_id=1220175919117893723&permissions=3072&scope=bot

using Discord;
using Discord.WebSocket;

namespace BG3WikiFetcher
{
    /// <summary>
    /// class containing functions to interface with discord
    /// </summary>
    public class DiscordHandler
    {
        //important discord ids
        private static ulong masterUserId = 637997156883628046;
        private static ulong settingsChannelId = 1220231791487615058;
        //static client object
        private static DiscordSocketClient discordClient;
        private static bool clientReady = false;
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
            clientReady = true;
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
            if (messageData.Channel.Id == settingsChannelId && messageData.Content.ToLower().StartsWith("r/"))
            {
                Console.WriteLine(messageData.Content.Substring(2));
                bool status = RedditHandler.ToggleSubreddit(messageData.Content);
                await ConfirmSubredditToggle(messageData.Content.Substring(2), status);
            }
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
        /// <summary>
        /// dm master user to confirm that a subreddit has been successfully toggled
        /// </summary>
        /// <param name="subredditName">name of the toggled subreddit</param>
        /// <param name="status"><subreddit's new listening status/param>
        private static async Task ConfirmSubredditToggle(string subredditName, bool status)
        {
            if (!clientReady) return;
            IUser user = await discordClient.GetUserAsync(masterUserId);
            IDMChannel channel = await user.CreateDMChannelAsync();
            await channel.SendMessageAsync(string.Format("listening status for r/{0} has been set to {1}", subredditName, status));
        }
    }
}
