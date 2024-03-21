using BG3WikiFetcher;
using System.Web;
using Newtonsoft.Json;
using Reddit;

//initialize program
Secrets secrets = Secrets.GetSecrets();
await Wiki.updatePages();
await RedditHandler.initialize(secrets);
await DiscordHandler.initialize(secrets);
//update the wiki pages every 24 hours
while (true)
{
    await Task.Delay(86400000);
    await Wiki.updatePages();
}