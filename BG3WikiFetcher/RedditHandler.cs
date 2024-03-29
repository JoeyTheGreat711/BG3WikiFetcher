﻿using System.Text;
using Reddit;
using Newtonsoft.Json;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using System.Net.Http.Headers;
using System.Xml;
using System.Text.RegularExpressions;
using System.Net;
using System.Diagnostics.CodeAnalysis;

namespace BG3WikiFetcher
{
    /// <summary>
    /// a class containg fucntions to interface with reddit's api
    /// </summary>
    public class RedditHandler
    {
        //important static urls
        private static string accessTokenUrl = "https://www.reddit.com/api/v1/access_token";
        private static string redirectUri = "http://localhost:8080";
        private static string aboutUrl = "https://www.reddit.com/user/BG3WikiFetcher/comments/1bk01df";
        //client objects
        private static HttpClient httpClient = new HttpClient();
        private static RedditClient redditClient;
        //important reddit objects
        private static List<string> subredditNames = new List<string>();
        private static List<Subreddit> subreddits = new List<Subreddit>();
        private static Queue<object> toReply = new Queue<object>();
        private static Dictionary<string, List<Comment>> seenComments = new Dictionary<string, List<Comment>>();
        private static Dictionary<string, List<Post>> seenPosts = new Dictionary<string, List<Post>>();
        private static List<string> blacklistedUsers = new List<string> { "bg3wikifetcher" }; //don't waste resources responding to its own comments
        private static List<string> masterUsers = new List<string> { "joeythegreat711" }; //users which can toggle subreddits to be listened to
        public static readonly string separator = "\n\n"; //reddit needs two line breaks to display text on a separate line
        public static readonly string botDisclaimer = string.Format("^This ^action ^was ^performed ^by ^a ^bot. [^(Learn more)]({0})", aboutUrl);
        /// <summary>
        /// get reddit access token, login, and start listening to comments
        /// </summary>
        public static async Task initialize(Secrets secrets)
        {
            //get api token
            string token = await getAccessToken(secrets);
            //login
            redditClient = new RedditClient(appId: secrets.redditId, appSecret: secrets.redditSecret, userAgent: "BG3WikiFetcher/v0.1 by joeythegreat711", accessToken: token);
            GetSubreddits();
            //don't respond to old comments or posts
            getCommentsAndPosts();
            toReply.Clear();
            Log("Logged in as " + redditClient.Account.Me.Name);
        }
        /// <summary>
        /// retrieves new comments and posts from relevant subreddits
        /// </summary>
        public static void getCommentsAndPosts()
        {
            foreach (Subreddit subreddit in subreddits)
            {
                if (!seenComments.ContainsKey(subreddit.Name))
                    seenComments.Add(subreddit.Name, new List<Comment>());
                if (!seenPosts.ContainsKey(subreddit.Name))
                    seenPosts.Add(subreddit.Name, new List<Post>());

                List<Comment> newComments = subreddit.Comments.GetNew(limit: 100);
                newComments.RemoveAll(x => blacklistedUsers.Contains(x.Author.ToLower()));
                seenComments[subreddit.Name].RemoveAll(x => !newComments.Exists(y => y.Id == x.Id)); //remove comments that no longer show up on the request from seen in order to save memory
                newComments.RemoveAll(x => seenComments[subreddit.Name].Exists(y => y.Id == x.Id)); //remove seen comments from new comments

                //same for posts
                List<Post> newPosts = subreddit.Posts.GetNew(limit: 100);
                newPosts.RemoveAll(x => blacklistedUsers.Contains(x.Author.ToLower()));
                seenPosts[subreddit.Name].RemoveAll(x => !newPosts.Exists(y => y.Id == x.Id));
                newPosts.RemoveAll(x => seenPosts[subreddit.Name].Exists(y => y.Id == x.Id));

                Log("new posts in r/" + subreddit.Name + ":");
                foreach (Post post in newPosts)
                {
                    Log(string.Format("r/{0} {1}", subreddit.Name, post.Title));
                }
                //queue up new comments and posts
                foreach (Comment comment in newComments)
                {
                    toReply.Enqueue(comment);
                    seenComments[subreddit.Name].Add(comment);
                }
                foreach (Post post in newPosts)
                {
                    toReply.Enqueue(post);
                    seenPosts[subreddit.Name].Add(post);
                }
            }
        }
        /// <summary>
        /// get body from either comment or post
        /// </summary>
        /// <param name="obj">comment or post object</param>
        /// <returns>body text of object</returns>
        private static string bodyOfAmbiguous(object obj)
        {
            if (obj is Comment)
                return (obj as Comment).Body;
            if (obj is Post)
                return (obj as SelfPost).SelfText;
            return "";
        }
        /// <summary>
        /// replies to the first relevant comment in the queue
        /// </summary>
        public static async Task replyToOne()
        {
            if (toReply.Count == 0) return;
            object nextReply = toReply.Dequeue();
            string? reply = await redditReply(bodyOfAmbiguous(nextReply));
            while (reply == null && toReply.Count > 0)
            {
                nextReply = toReply.Dequeue();
                reply = await redditReply(bodyOfAmbiguous(nextReply));
            }
            if (reply != null)
            {
                if (nextReply is Comment)
                {
                    Log("replying to " + (nextReply as Comment).Permalink);
                    await (nextReply as Comment).ReplyAsync(reply);
                }
                if (nextReply is Post)
                {
                    Log("replying to " + (nextReply as Post).Permalink);
                    await (nextReply as Post).ReplyAsync(reply);
                }
            }
        }
        /// <summary>
        /// handler method for comment listener (not used right now)
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="args">comment info</param>
        private static async void commentReceived(object? sender, CommentsUpdateEventArgs args)
        {
            try
            {
                //reply to all comments
                foreach (Comment comment in args.Added)
                {
                    if (!subredditNames.Contains(comment.Subreddit.ToLower())) continue;
                    if (blacklistedUsers.Contains(comment.Author.ToLower())) continue;
                    string? reply = await redditReply(comment.Body);
                    if (reply == null) return;
                    await comment.ReplyAsync(reply);
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }
        /// <summary>
        /// generate response formatted for a reddit comment containing links to all mentioned pages
        /// </summary>
        /// <param name="commentBody">entire body of reddit comment</param>
        /// <returns>string to be sent, or null if no pages were found</returns>
        public static async Task<string?> redditReply(string commentBody)
        {
            List<Page> pages = Wiki.findPages(commentBody);
            return await Wiki.reply(pages, Wiki.ReplyType.Reddit);
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
            subreddits = subredditNames.Select(x => redditClient.Subreddit(x)).ToList();
        }
        /// <summary>
        /// save subreddits to subreddits.json
        /// </summary>
        private static void SetSubreddits()
        {
            string json = JsonConvert.SerializeObject(subredditNames, Newtonsoft.Json.Formatting.Indented);
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
            subredditNames.Add(subredditName);
            subreddits.Add(redditClient.Subreddit(subredditName));
            SetSubreddits();
            return subredditNames.Contains(subredditName);
        }
    }
}
