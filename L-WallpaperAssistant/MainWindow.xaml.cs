using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml;

namespace L_WallpaperAssistant
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int wallpaperIndex = 0;
        private string downloadDir = AppDomain.CurrentDomain.BaseDirectory + "download";
        private Timer downloadTimer = new Timer();
        private Timer wallpaperTimer = new Timer();

        public MainWindow()
        {
            InitializeComponent();
            init();
        }

        private async Task<Dictionary<int, string>> getPictureUrlList(int index = 0, int number = 10, int width = 1920, int height = 1080)
        {
            string url = string.Format("http://www.bing.com/HPImageArchive.aspx?format=xml&idx={0}&n={1}&mkt=en-US", index, number);
            string imageBaseUrl = "http://www.bing.com";
            Dictionary<int, string> urlList = new Dictionary<int, string>();
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string xmlStr = await response.Content.ReadAsStringAsync();
                if (xmlStr.Contains("image"))
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

        private async Task downloadImages(Dictionary<int, string> imageUrlList, string saveDir)
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


        private async void init()
        {
            await getWallpapers();
            setNextWallpaper();
            downloadTimer.Interval = 4 * 3600 * 1000;
            wallpaperTimer.Interval = 0.5 * 3600 * 1000;
            downloadTimer.Elapsed += DownloadTimer_Elapsed;
            wallpaperTimer.Elapsed += WallpaperTimer_Elapsed;
            downloadTimer.Start();
            wallpaperTimer.Start();
        }

        private void WallpaperTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            wallpaperTimer.Stop();
            setNextWallpaper();
            wallpaperTimer.Start();
        }

        private async void DownloadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            downloadTimer.Stop();
            await getWallpapers();
            downloadTimer.Start();
        }

        private void setNextWallpaper()
        {
            var imagePathList = Directory.GetFiles(downloadDir);
            DesktopWallpaper.SetDesktopBackground(imagePathList[wallpaperIndex], "Stretched");
            wallpaperIndex = (wallpaperIndex + 1) % imagePathList.Length;
        }

        private async Task getWallpapers()
        {
            var imageUrlList = await getPictureUrlList();
            await downloadImages(imageUrlList, downloadDir);
        }

        private void nextWallpaperButtonClick(object sender, RoutedEventArgs e)
        {
            nextWallpaperButton.IsEnabled = false;
            setNextWallpaper();
            nextWallpaperButton.IsEnabled = true;
        }

        private async void updateWallpapersButtonClick(object sender, RoutedEventArgs e)
        {
            updateWallpapersButton.IsEnabled = false;
            await getWallpapers();
            updateWallpapersButton.IsEnabled = true;
        }
    }
}
