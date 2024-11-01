using BG3WikiFetcher;
using System.Text;

//initialize program
await Wiki.updatePages();

Sheets.hyperLinkCells("C:/Users/joeye/Downloads/CSV test.xlsx", 0, "D15", "D348");
Sheets.hyperLinkCells("C:/Users/joeye/Downloads/CSV test.xlsx", 1, "D17", "D284");
Sheets.hyperLinkCells("C:/Users/joeye/Downloads/CSV test.xlsx", 2, "D15", "D437");