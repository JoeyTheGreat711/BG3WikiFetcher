using System.Text;
using System.Web;
using System.Xml;
using FastFuzzyStringMatcher;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;

namespace BG3WikiFetcher
{
    /// <summary>
    /// class containing functions to get, parse, and search page data
    /// </summary>
    public class Wiki
    {
        //needed wiki urls
        public static string baseUrl = "https://bg3.wiki/wiki/";
        private static string sitemapUrl = "https://bg3.wiki/sitemap/sitemap-bg3wiki-NS_0-0.xml";
        private static List<string> tableUrls = new List<string>
        {
            "https://bg3.wiki/w/api.php?action=cargoquery&tables=weapons&fields=name&where=rarity%20%3C%3E%20%27common%27&limit=500&format=json",
            "https://bg3.wiki/w/api.php?action=cargoquery&tables=equipment&fields=name&where=rarity%20%3C%3E%20%27common%27&limit=500&format=json",
            "https://bg3.wiki/w/api.php?action=cargoquery&tables=misc_items&fields=name&where=rarity%20%3C%3E%20%27common%27&limit=500&format=json",
            "https://bg3.wiki/w/api.php?action=cargoquery&tables=spells&fields=name&limit=500&format=json&offset=499"
        };

        //static objects necessary for search
        public static Dictionary<string, List<Page>> allPages = new Dictionary<string, List<Page>>();
        private static StringMatcher<string> stringMatcher = new StringMatcher<string>(MatchingOption.None);
        public static EditDistanceCalculator editDistanceCalculator = new EditDistanceCalculator();
        /// <summary>
        /// downloads, parses, and saves data from the BG3 wiki
        /// </summary>
        /// <returns></returns>
        public static async Task updatePages()
        {
            //clear local data
            stringMatcher = new StringMatcher<string>(MatchingOption.None);
            allPages.Clear();
            //load sitemap xml file
            HttpClient wikiClient = new HttpClient();
            XmlDocument sitemap = new XmlDocument();
            HttpResponseMessage response = await wikiClient.GetAsync(sitemapUrl);
            sitemap.LoadXml(await response.Content.ReadAsStringAsync());
            
            /*  xml document expected structure:
             *  <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
             *      <url>
             *          <loc>https://bg3.wiki/wiki/Page_Name</loc>
             *          <lastmod>2024-03-12T19:14:55Z</lastmod>
             *          <priority>1.0</priority>
             *      </url>
             *      ...
             *  </urlset>
             */
            //loop through all urls
            foreach (XmlNode node in sitemap.LastChild.ChildNodes.Cast<XmlNode>().Skip(1))
            {
                //get url from node
                string url = node.FirstChild.InnerText;
                //if (url.Count(x => x == '/') == 4) //this works to ignore sub-pages which would cause duplicate errors
                //{
                    //create new Page object from url
                    Page p = new Page(url);
                    //either create new search name or add to existing one
                    if (allPages.ContainsKey(p.searchName))
                        allPages[p.searchName].Add(p);
                    else
                        allPages.Add(p.searchName, new List<Page> { p });
                    //add search name to BK-tree
                    stringMatcher.Add(p.searchName, "");
                //}
                //else
                //    Log("discarded " + url);
            }

            //find specific pages
            foreach (string s in tableUrls)
            {
                int offset = 0;
                while (true)
                {
                    response = await wikiClient.GetAsync(s + "&offset=" + offset);
                    Log(await response.Content.ReadAsStringAsync());
                    CargoTable? cargoTable = JsonConvert.DeserializeObject<CargoTable>(await response.Content.ReadAsStringAsync());
                    if (cargoTable == null) break;
                    foreach(dynamic d in cargoTable.cargoquery)
                    {
                        //Log("decoding: " + d.title.name);
                        string pageTitle = HttpUtility.HtmlDecode("" + d.title.name);
                        string searchName = Page.standardizeSearch(pageTitle);
                        if (allPages.ContainsKey(searchName) && allPages[searchName].Exists(x => x.title == pageTitle))// && pageTitle.Length >= 6) //minimum length of 6 to avoid generic pages like net
                        {
                            int index = allPages[searchName].FindIndex(x => x.title == pageTitle);
                            allPages[searchName][index].isSpecific = true;
                            //Log(pageTitle + " is specific");
                        }
                    }
                    offset += 500;
                    if (cargoTable.cargoquery.Count < 500) break;
                }
            }
            Log(string.Format("updated wiki, found {0} pages", allPages.Count));
        }
        /// <summary>
        /// searches for closest-matching page title from user input
        /// </summary>
        /// <param name="input">as-typed user input</param>
        /// <returns>page object containing all necessary information about closest page, or null if none was found</returns>
        public static Page? findPage(string input, float matchPercentage)
        {
            //standardize user input for initial search, since page names were also standardized
            string standardizedInput = Page.standardizeSearch(input);
            //search for closest matching search name
            SearchResultList<string> results =  stringMatcher.Search(standardizedInput, matchPercentage);
            //return null if no page was found
            if (results.Count == 0) return null;
            //get actual closest set of matching pages
            results.SortByClosestMatch();
            string result = results[0].Keyword;
            List<Page> pages = allPages[result];
            //if page has a unique search name
            if (pages.Count == 1)
                return pages[0];
            //if multiple pages share the same search name, return closest actual title to actual user input
            return pages.MinBy(x => editDistanceCalculator.CalculateEditDistance(input, x.title));
        }
        /// <summary>
        /// gets all pages mentioned in comment in [[double brackets]]
        /// </summary>
        /// <param name="input">entire input string from user</param>
        /// <returns>list of pages</returns>
        public static List<Page> findPages(string input)
        {
            //any string ignoring starting and ending whitespace inside [[double brackets]]
            Regex regex = new Regex(@"\\?\[\\?\[\s*(.+?)\s*\\?\]\\?\]");

            //find all mentioned pages
            List<Page> pages = new List<Page>();
            foreach (Match match in regex.Matches(input))
            {
                string search = match.Groups[1].Value;
                //Log("searching " + search);
                Page? page = findPage(search, 75f);
                if (page != null && !pages.Contains(page))
                    pages.Add(page);
            }
            return pages;
        }
        /// <summary>
        /// gets all pages mentioned in comment if they are considered specific
        /// </summary>
        /// <param name="input">entire input string from user</param>
        /// <returns></returns>
        public static List<Page> findSpecificPages(string input)
        {
            List<string> words = input.Split().ToList();
            List<Page> pages = new List<Page>();
            int longestTitle = 0;
            foreach(KeyValuePair<string, List<Page>> pair in allPages)
            {
                foreach(Page p in pair.Value)
                {
                    if (p.isSpecific && p.title.Split().Length > longestTitle)
                        longestTitle = p.title.Split().Length;
                }
            }
            for (int length = Math.Min(longestTitle, words.Count); length >= 1; length--)
            {
                for (int left = 0; left < words.Count - length + 1; left++)
                {
                    string searchText = string.Join(' ', words.GetRange(left, length));
                    Page? page = findPage(searchText, 90f);
                    if (page != null && !pages.Contains(page) && page.isSpecific)
                    {
                        Log(string.Format("Found page: \"{0}\" from text: \"{1}\"", page.title, string.Join(" ", words.GetRange(left, length))));
                        pages.Add(page);
                        words.RemoveRange(left, length);
                        left--;
                    }
                    //else
                    //    Log(string.Format("Found nothing from text: {0}", string.Join(" ", words.GetRange(left, length))));
                }
            }
            return pages;
        }
        /// <summary>
        /// fetch description of a wiki page
        /// </summary>
        /// <param name="page">target page</param>
        /// <returns>decoded description as string</returns>
        private static async Task<string?> getDescription(Page page)
        {
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(page.getUrl());
            string responseContent = await response.Content.ReadAsStringAsync();
            string pattern = "<meta name=\"description\" content=\"(.*?)\"/>";
            return WebUtility.HtmlDecode(Regex.Match(responseContent, pattern).Groups[1].Value);
        }
        /// <summary>
        /// generate a reply string
        /// </summary>
        /// <param name="pages">list of pages requested</param>
        /// <param name="replyType">platform requesting the reply</param>
        /// <returns>reply string formatted for the correct platform</returns>
        public static async Task<string?> reply(List<Page> pages, ReplyType replyType)
        {
            if (pages.Count == 0) return null;
            string separator = "";
            if (replyType == ReplyType.Discord) separator = DiscordHandler.separator;
            if (replyType == ReplyType.Reddit) separator = RedditHandler.separator;
            string reply = "";
            if (pages.Count == 1)
            {
                Page page = pages[0];
                string url = page.getUrl();
                if (replyType == ReplyType.Discord)
                    url = "<" + url + ">";
                reply += string.Format("[{0}]({1})", page.title, url);
                string description = await Wiki.getDescription(pages[0]) + separator;
                if (description.StartsWith(page.title))
                    description = description.Substring(page.title.Length);
                else
                    reply += "\n\n";
                reply += description;
            }
            else
            {
                foreach (Page page in pages)
                {
                    string url = page.getUrl();
                    if (replyType == ReplyType.Discord)
                        url = "<" + url + ">";
                    reply += string.Format("[{0}]({1}){2}", page.title, url, separator);
                }
            }
            if (replyType == ReplyType.Reddit)
                reply += RedditHandler.botDisclaimer;
            return reply;
        }
        public enum ReplyType
        {
            Discord,
            Reddit
        }
        /// <summary>
        /// prints log message to console with wiki flag
        /// </summary>
        /// <param name="message">message to be printed</param>
        private static void Log(string message)
        {
            Console.WriteLine("[Wiki] " + message);
        }
    }
    /// <summary>
    /// class containing info about a particular wiki page
    /// </summary>
    public class Page
    {
        //a list of strings which can be ignored when at the start of a string
        private static List<string> ignorableStarts = new List<string>()
        {
            "the ",
            "a ",
            "on ",
            "legendary action "
        };
        //a list of strings which can be ignored when at the end of a sring
        private static List<string> ignorableEnds = new List<string>()
        {
            " condition"
        };
        //substitutions to make in the middle of a string
        private static Dictionary<string, string> substitutions = new Dictionary<string, string>()
        {
            { " of ", " " },
            { " the ", " " },
            { "&", "and" }
        };
        /// <summary>
        /// standardize strings for search so that things like capitalization or special
        /// characters similar to letters don't penalize the levenshtein distance
        /// </summary>
        /// <param name="str">string to be standardized</param>
        /// <returns>string with ignorable substrings removed, special characters replaced, and all lowercase</returns>
        public static string standardizeSearch(string str)
        {
            //deal with sup-pages ("raphael/combat" becomes "raphael combat", etc.)
            str = str.Replace("/", " ");
            //convert to lowercase
            str = str.ToLower();
            //replace special characters with alphabetic equivalent, for instance û becomes u
            str = str.Normalize(NormalizationForm.FormD);
            str = Regex.Replace(str, @"[^a-zA-Z0-9\s]", "");
            //replace/remove substrings
            foreach (string s in ignorableStarts)
            {
                while (str.StartsWith(s))
                    str = str.Substring(s.Length);
            }
            foreach (string s in ignorableEnds)
            {
                while (str.EndsWith(s))
                    str = str.Substring(0, str.Length - s.Length);
            }
            foreach (KeyValuePair<string, string> pair in substitutions)
                str = str.Replace(pair.Key, pair.Value);
            //some previous operations may havre created consecutive spaces, so we remove those
            while (str.Contains("  "))
                str = str.Replace("  ", " ");
            //this will make searching "brilliance boots" valid to find "boots of brilliance", etc.
            str = string.Join(" ", str.Split(' ').Order());
            return str;
        }
        //constructor
        public Page(string url)
        {
            this.urlExtension = string.Join("/", url.Split('/').Skip(4));
            this.title = HttpUtility.UrlDecode(this.urlExtension).Replace("_", " ");
            this.searchName = standardizeSearch(this.title);
            this.isSpecific = false;
        }
        //title of page as it appears on the wiki
        public string title { get; set; }
        //standardized title for accurate searches
        public string searchName { get; set; }
        //unique portion of url
        public string urlExtension { get; set; }
        //non-specific pages can only be searched in [[double brackets]]
        public bool isSpecific;
        //get full url
        public string getUrl()
        {
            return Wiki.baseUrl + this.urlExtension;
        }
        //page report for debugging
        public string getReport()
        {
            return "   " + string.Join("\n   ", new string[] { searchName, title, getUrl() });
        }
    }
}
