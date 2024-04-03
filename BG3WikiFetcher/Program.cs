using BG3WikiFetcher;
using System.Text;

//initialize program
Secrets secrets = Secrets.GetSecrets();
await Wiki.updatePages();

Console.WriteLine(string.Join("\n\n", Wiki.allPages.Where(x => x.Value.Count > 1).OrderBy(x => x.Key).Select(x => x.Key + "\n   " + string.Join("\n   ", x.Value.Select(x => x.urlExtension)))));
//while (true)
//    Console.WriteLine(Page.standardizeSearch(Console.ReadLine()));

while (true)
{
    Console.Write("enter search input: ");
    Page? page = Wiki.findPage(Console.ReadLine());
    if (page == null)
        Console.WriteLine("no page found");
    else
        Console.WriteLine("found: \n" + page.getReport());
}

/*
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
*/