using System.Text;
using System.Web;
using System.Xml;
using FastFuzzyStringMatcher;
using System.Text.RegularExpressions;

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
                if (url.Count(x => x == '/') == 4) //this works to ignore sub-pages which would cause duplicate errors
                {
                    //create new Page object from url
                    Page p = new Page(url);
                    //either create new search name or add to existing one
                    if (allPages.ContainsKey(p.searchName))
                        allPages[p.searchName].Add(p);
                    else
                        allPages.Add(p.searchName, new List<Page> { p });
                    //add search name to BK-tree
                    stringMatcher.Add(p.searchName, "");
                }
            }
        }
        /// <summary>
        /// searches for closest-matching page title from user input
        /// </summary>
        /// <param name="input">as-typed user input</param>
        /// <returns>page object containing all necessary information about closest page, or null if none was found</returns>
        public static Page? findPage(string input)
        {
            //standardize user input for initial search, since page names were also standardized
            string standardizedInput = Page.standardizeSearch(input);
            //search for closest matching search name
            SearchResultList<string> results =  stringMatcher.Search(standardizedInput, 75f);
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
            "on "
        };
        //substitutions to make in the middle of a string
        private static Dictionary<string, string> substitutions = new Dictionary<string, string>()
        {
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
            //convert to lowercase
            str = str.ToLower();
            //remove ignorable substrings
            foreach (string s in ignorableStarts)
            {
                if (str.StartsWith(s))
                    str = str.Substring(s.Length);
            }
            foreach (KeyValuePair<string, string> pair in substitutions)
                str = str.Replace(pair.Key, pair.Value);
            //replace special characters with alphabetic equivalent, for instance û becomes u
            str = str.Normalize(NormalizationForm.FormD);
            str = Regex.Replace(str, @"[^a-zA-Z0-9\s]", "");
            //some previous operations may havre created consecutive spaces, so we remove those
            while (str.Contains("  "))
                str = str.Replace("  ", " ");
            return str;
        }
        //constructor
        public Page(string url)
        {
            this.urlExtension = url.Split('/')[4];
            this.title = HttpUtility.UrlDecode(this.urlExtension).Replace("_", " ");
            this.searchName = standardizeSearch(this.title);
        }
        //title of page as it appears on the wiki
        public string title { get; set; }
        //standardized title for accurate searches
        public string searchName { get; set; }
        //unique portion of url
        public string urlExtension { get; set; }
        //get full url
        public string getUrl()
        {
            return Wiki.baseUrl + this.urlExtension;
        }
    }
}
