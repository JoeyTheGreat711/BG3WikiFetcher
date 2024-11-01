using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;

namespace BG3WikiFetcher
{
    /// <summary>
    /// class containing functions to read and write xlsx files IOT hyperlink certain cells to link to the bg3.wiki
    /// </summary>
    public class Sheets
    {
        private static string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        /// <summary>
        /// function to add hyperlinks to all relevant cells in specified area
        /// </summary>
        /// <param name="filePath">entire path of xlsx file to read and write</param>
        /// <param name="topLeft">name of the top left cell of the area to be edited, for instance "A1"</param>
        /// <param name="bottomRight">name of the bottom right cell of the area to be edited, for instance "Z10"</param>
        public static void hyperLinkCells(string filePath, int sheetIndex, string topLeft, string bottomRight)
        {
            XLWorkbook workbook = new XLWorkbook(filePath);
            IXLWorksheet worksheet = workbook.Worksheets.ToList()[sheetIndex];
            Log("sheet: " + worksheet.Name);
            for (int x = alphabet.IndexOf(topLeft[0]); x <= alphabet.IndexOf(bottomRight[0]); x++)
            {
                for (int y = int.Parse(topLeft.Substring(1)); y <= int.Parse(bottomRight.Substring(1)); y++)
                {
                    IXLCell cell = worksheet.Cell("" + alphabet[x] + y);
                    if (cell.Value.Type != XLDataType.Text) continue;
                    Page ? wikiPage = Wiki.findPage(cell.Value.GetText());
                    if (wikiPage == null) continue;
                    cell.SetHyperlink(new XLHyperlink(wikiPage.getUrl()));
                    Log(string.Format("setting hyperlink at {0}\n   text: {1}\n   address: {2}", cell.Address, wikiPage.title, wikiPage.getUrl()));
                }
            }
            workbook.Save();
        }
        /// <summary>
        /// prints log message to console with sheets flag
        /// </summary>
        /// <param name="message">message to be printed</param>
        private static void Log(string message)
        {
            Console.WriteLine("[Sheets] " + message);
        }
    }
}
