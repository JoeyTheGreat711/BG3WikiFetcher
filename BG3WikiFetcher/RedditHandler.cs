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
        //client objects
        private static HttpClient httpClient = new HttpClient();
        private static RedditClient redditClient;
        //important reddit strings
        private static List<string> subredditNames = new List<string> { "testingground4bots" };
        private static List<string> blacklistedUsers = new List<string> { "BG3WikiFetcher" }; //don't waste resources responding to its own comments
        /// <summary>
        /// get reddit access token, login, and start listening to comments
        /// </summary>
        public static async Task initialize()
        {
            //load secrets from secrets.json
            Secrets secrets = GetSecrets();
            //get api token
            string token = await getAccessToken(secrets);
            //login
            redditClient = new RedditClient(appId: secrets.redditId, appSecret: secrets.redditSecret, userAgent: "BG3WikiFetcher/v0.1 by joeythegreat711", accessToken: token);
            Console.WriteLine("Logged in as " + redditClient.Account.Me.Name);
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
        private static void commentRecieved(object? sender, CommentsUpdateEventArgs args)
        {
            //loop through all new comments
            foreach (Comment comment in args.Added)
            {
                //reply to comment not yet implemented
            }
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
        /// read sec5rets.json to get secrets such as id, password, etc.
        /// </summary>
        /// <returns>object containing secrets</returns>
        private static Secrets GetSecrets()
        {
            //read file
            StreamReader sr = new StreamReader(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/secrets.json");
            string secretsJson = sr.ReadToEnd();
            sr.Close();
            //parse and return secrets
            return JsonConvert.DeserializeObject<Secrets>(secretsJson);
        }
    }
}
