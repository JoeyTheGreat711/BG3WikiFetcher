using BG3WikiFetcher;
using System.Diagnostics;
using System.Text;

//initialize program
Secrets secrets = Secrets.GetSecrets();
await Wiki.updatePages();

await RedditHandler.initialize(secrets);
await DiscordHandler.initialize(secrets);

//request new comments every minute, reply to one comment every 5 seconds
//also update the wiki pages every 24 hours
long nextCommentsUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
long nextWikiUpdte = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400;
while (true)
{
    await Task.Delay(5000);
    await RedditHandler.replyToOne();
    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    if (now >= nextCommentsUpdate)
    {
        RedditHandler.getCommentsAndPosts();
        nextCommentsUpdate = now + 60;
    }
    if (now >= nextWikiUpdte)
    {
        await Wiki.updatePages();
        nextWikiUpdte = now + 86400;
    }
}