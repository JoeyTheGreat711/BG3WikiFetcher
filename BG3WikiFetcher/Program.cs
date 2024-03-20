using BG3WikiFetcher;
using System.Web;
using Newtonsoft.Json;
using Reddit;

//initialize program
await Wiki.updatePages();
await RedditHandler.initialize();
//infinite delay so that program does not exit
await Task.Delay(-1);