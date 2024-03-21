using System.Text;
using Reddit;
using Newtonsoft.Json;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace BG3WikiFetcher
{
    /// <summary>
    /// a class containg fucntions to interface with reddit's api
    /// </summary>
    public class RedditHandler
    {
        //important static urls
        private static string accessTokenUrl = "https://www.reddit.com/api/v1/access_token";
        private static string authUrl = "https://www.reddit.com/api/v1/authorize?";
        private static string redirectUri = "http://localhost:8080";
        private static string aboutUrl = "https://www.reddit.com/u/BG3WikiFetcher";
        //client objects
        private static HttpClient httpClient = new HttpClient();
        private static RedditClient redditClient;
        //important reddit strings
        private static List<string> subredditNames = new List<string> { "testingground4bots" };
        private static List<string> blacklistedUsers = new List<string> { "BG3WikiFetcher" }; //don't waste resources responding to its own comments
        /// <summary>
        /// get reddit access token, login, and start listening to comments
        /// </summary>
        public static async Task initialize(Secrets secrets)
        {
            //get api token
            string token = await getAccessToken(secrets);
            //login
            redditClient = new RedditClient(appId: secrets.redditId, appSecret: secrets.redditSecret, userAgent: "BG3WikiFetcher/v0.1 by joeythegreat711", accessToken: token);
            Log("Logged in as " + redditClient.Account.Me.Name);
            //start listening to comments
            List<Subreddit> subreddits = subredditNames.Select(x => redditClient.Subreddit(x)).ToList();
            foreach (Subreddit subreddit in subreddits)
            {
                subreddit.Comments.GetNew();
                subreddit.Comments.MonitorNew();
                subreddit.Comments.NewUpdated += commentRecieved;
            }
        }
        /// <summary>
        /// handler method for comment listener
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="args">comment info</param>
        private static async void commentRecieved(object? sender, CommentsUpdateEventArgs args)
        {
            //reply to all comments
            foreach (Comment comment in args.Added)
            {
                if (blacklistedUsers.Contains(comment.Author)) continue;
                string? reply = redditReply(comment.Body);
                if (reply == null) return;
                await comment.ReplyAsync(reply);
            }
        }
        /// <summary>
        /// generate response formatted for a reddit comment containing links to all mentioned pages
        /// </summary>
        /// <param name="commentBody">entire body of reddit comment</param>
        /// <returns>string to be sent, or null if no pages were found</returns>
        public static string? redditReply(string commentBody)
        {
            List<Page> pages = Wiki.findPages(commentBody);
            if (pages.Count == 0) return null;
            string reply = "";
            foreach (Page p in pages)
                reply += string.Format("[{0}]({1})\n\n", p.title, p.getUrl());
            reply += string.Format("^This ^action ^was ^performed ^by ^a ^bot. ^[Usage]({0})", aboutUrl);
            return reply;
        }
        /// <summary>
        /// make http request to get reddit api token
        /// </summary>
        /// <param name="secrets">object containing secrets</param>
        /// <returns>access token for reddit api</returns>
        private static async Task<string> getAccessToken(Secrets secrets)
        {
            //create http request
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, accessTokenUrl);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "username", "BG3WikiFetcher" },
                { "password", secrets.redditPass }
            });
            request.Headers.Add("User-Agent", "wikifetcher/v0.1 by joeythegreat711");
            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(secrets.redditId + ":" + secrets.redditSecret));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            //send http request
            HttpResponseMessage response = await httpClient.SendAsync(request);
            //parse and return response
            return JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync()).access_token;
        }
        /// <summary>
        /// print log to console with reddit flag
        /// </summary>
        /// <param name="msg">log message</param>
        private static void Log(string msg)
        {
            Console.WriteLine("[Reddit] " + msg);
        }
    }
}
