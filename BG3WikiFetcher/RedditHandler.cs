using System.Text;
using Reddit;
using Newtonsoft.Json;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using System.Net.Http.Headers;

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
        private static string aboutUrl = "https://www.reddit.com/user/BG3WikiFetcher/comments/1bk01df";
        //client objects
        private static HttpClient httpClient = new HttpClient();
        private static RedditClient redditClient;
        //important reddit strings
        private static List<string> subredditNames = new List<string>();
        private static List<string> blacklistedUsers = new List<string> { "bg3wikifetcher" }; //don't waste resources responding to its own comments
        private static List<string> masterUsers = new List<string> { "joeythegreat711" }; //users which can toggle subreddits to be listened to
        /// <summary>
        /// get reddit access token, login, and start listening to comments
        /// </summary>
        public static async Task initialize(Secrets secrets)
        {
            //get api token
            string token = await getAccessToken(secrets);
            //login
            redditClient = new RedditClient(appId: secrets.redditId, appSecret: secrets.redditSecret, userAgent: "BG3WikiFetcher/v0.1 by joeythegreat711", accessToken: token);
            //start listening to comments
            GetSubreddits();
            List<Subreddit> subreddits = subredditNames.Select(x => redditClient.Subreddit(x)).ToList();
            foreach (Subreddit subreddit in subreddits)
            {
                subreddit.Comments.GetNew();
                subreddit.Comments.MonitorNew();
                subreddit.Comments.NewUpdated += commentRecieved;
            }
            Log("Logged in as " + redditClient.Account.Me.Name);
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
                if (!subredditNames.Contains(comment.Subreddit.ToLower())) continue;
                if (blacklistedUsers.Contains(comment.Author.ToLower())) continue;
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
        /// <summary>
        /// load subreddits from subreddits.json
        /// </summary>
        private static void GetSubreddits()
        {
            StreamReader sr = new StreamReader(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/subreddits.json");
            string json = sr.ReadToEnd();
            sr.Close();
            subredditNames = JsonConvert.DeserializeObject<List<string>>(json);
        }
        /// <summary>
        /// save subreddits to subreddits.json
        /// </summary>
        private static void SetSubreddits()
        {
            string json = JsonConvert.SerializeObject(subredditNames, Formatting.Indented);
            StreamWriter sr = new StreamWriter(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/subreddits.json");
            sr.Write(json);
            sr.Close();
        }
        /// <summary>
        /// toggle whether to listen to a subreddit
        /// </summary>
        /// <param name="subredditName">name of subreddit to be toggled</param>
        public static bool ToggleSubreddit(string subredditName)
        {
            GetSubreddits();
            bool containsSubreddit = subredditNames.Contains(subredditName);
            Subreddit subreddit = redditClient.Subreddit(subredditName);
            if (containsSubreddit)
            {
                subreddit.Comments.NewUpdated -= commentRecieved;
                subreddit.Comments.KillAllMonitoringThreads();
                subredditNames.Remove(subredditName);
            }
            else
            {
                subreddit.Comments.MonitorNew();
                subreddit.Comments.NewUpdated += commentRecieved;
                subredditNames.Add(subredditName);
            }
            SetSubreddits();
            return !containsSubreddit;
        }
    }
}
