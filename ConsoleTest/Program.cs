using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Xml;
using System.Text.RegularExpressions;
using System.IO;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Action action = async () => 
            {
                var urlList = await GetPictureUrlList();
                await DownloadImages(urlList, AppDomain.CurrentDomain.BaseDirectory);
            };
            action();
            Console.ReadKey();
        }

        static async Task<Dictionary<int, string>> GetPictureUrlList(int width = 1920, int height = 1080)
        {
            Dictionary<int, string> urlList = new Dictionary<int, string>();
            string url = "http://www.bing.com/HPImageArchive.aspx?format=xml&idx=0&n=8&mkt=en-US";
            string imageBaseUrl = "http://www.bing.com";
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url);
            if(response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string xmlStr = await response.Content.ReadAsStringAsync();
                if(xmlStr.Contains("image"))
                {
                    XmlDocument xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(xmlStr);
                    XmlNodeList xmlNodeList = xmlDocument.GetElementsByTagName("image");
                    foreach (XmlNode xmlNode in xmlNodeList)
                    {
                        string imageUrl = string.Format("{0}{1}", imageBaseUrl, xmlNode.SelectSingleNode("url").InnerText);
                        int startDate = int.Parse(xmlNode.SelectSingleNode("startdate").InnerText);
                        Regex regex = new Regex(@"\d+x\d+");
                        imageUrl = regex.Replace(imageUrl, string.Format("{0}x{1}", width, height));
                        urlList.Add(startDate, imageUrl);
                    }
                }
            }
            return urlList;
        }

        static async Task DownloadImages(Dictionary<int, string> imageUrlList, string saveDir)
        {
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
            saveDir = saveDir.TrimEnd('\\') + "\\";
            HttpClient client = new HttpClient();
            foreach (var url in imageUrlList)
            {
                string fileName = string.Format("{0}_{1}", url.Key, Path.GetFileName(url.Value));
                
                using (FileStream fs = File.Create(string.Format("{0}{1}", saveDir, fileName)))
                {
                    using (Stream stream = await client.GetStreamAsync(url.Value))
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = 0;
                        do
                        {
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                            await fs.WriteAsync(buffer, 0, bytesRead);
                        } while (bytesRead > 0);
                    }
                }

            }
        }
    }
}
