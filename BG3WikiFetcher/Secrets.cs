using Newtonsoft.Json;

namespace BG3WikiFetcher
{
    /// <summary>
    /// binding class for secrets.json
    /// </summary>
    public class Secrets
    {
        public string redditSecret;
        public string redditId;
        public string redditPass;
        public string discordToken;
        /// <summary>
        /// read secrets.json to get secrets such as id, password, etc.
        /// </summary>
        /// <returns>object containing secrets</returns>
        public static Secrets GetSecrets()
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