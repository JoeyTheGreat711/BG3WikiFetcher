using BG3WikiFetcher;
using System.Web;
using Newtonsoft.Json;

//load wiki pages
await Wiki.updatePages();
//find and print pages with identical search names
Dictionary<string, List<Page>> duplicateSearchNames =  new Dictionary<string, List<Page>>();
foreach (KeyValuePair<string, List<Page>> pair in Wiki.allPages.Where(x => x.Value.Count > 1))
{
    Console.WriteLine(pair.Key);
    foreach (Page p in pair.Value)
        Console.WriteLine("    " + p.urlExtension);
}
//search testing (can be removed)
while (true)
{
    Console.Write("Search input: ");
    string input = Console.ReadLine();
    Page result = Wiki.findPage(input);
    if (result == null)
    {
        Console.WriteLine("no result found");
    }
    else
    {
        Console.WriteLine("  Page title: " + result.title);
        Console.WriteLine("    Page url: " + result.getUrl());
    }
}