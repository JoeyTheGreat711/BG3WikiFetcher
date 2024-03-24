using BG3WikiFetcher;

//initialize program
Secrets secrets = Secrets.GetSecrets();
await Wiki.updatePages();
await RedditHandler.initialize(secrets);
await DiscordHandler.initialize(secrets);

//update the wiki pages every 24 hours and re-initialize reddit every 6 hours
while (true)
{
    for (int i = 0; i < 4; i++)
    {
        await Task.Delay(21600000);
        await RedditHandler.initialize(secrets);
    }
    await Wiki.updatePages();
}