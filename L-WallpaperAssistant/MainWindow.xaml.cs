using System;
using System.Collections.Generic;
using System.Configuration;
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
        private string tempDir = AppDomain.CurrentDomain.BaseDirectory + "temp";
        private Timer downloadTimer = new Timer();
        private Timer wallpaperTimer = new Timer();
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private bool isClosing = false;
        private bool isHidden = false;

        private bool isRandom;
        private int fetchNumber;
        private int fetchIndex;
        private double downloadInterval;
        private double wallpaperChangeInterval;
        private int wallpaperWidth;
        private int wallpaperHeight;

        private delegate void wallpaperUpdateHandler();
        private event wallpaperUpdateHandler wallpaperUpdating;
        private event wallpaperUpdateHandler wallpaperUpdated;



        public MainWindow()
        {
            InitializeComponent();
            init();
        }

        private async Task<Dictionary<int, string>> getPictureUrlList(int index = 0, int number = 8, int width = 1920, int height = 1080)
        {
            string url = string.Format("http://www.bing.com/HPImageArchive.aspx?format=xml&idx={0}&n={1}&mkt=en-US", index, number);
            string imageBaseUrl = "http://www.bing.com";
            Dictionary<int, string> urlList = new Dictionary<int, string>();
            HttpClient client = new HttpClient();
            try
            {
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
            }
            catch (Exception)
            {
                //TODO write error log
            }


            return urlList;
        }

        private async Task downloadImages(Dictionary<int, string> imageUrlList, string saveDir, string tempDir)
        {
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            saveDir = saveDir.TrimEnd('\\') + "\\";
            tempDir = tempDir.TrimEnd('\\') + "\\";
            HttpClient client = new HttpClient();
            try
            {
                foreach (var url in imageUrlList)
                {
                    string fileName = string.Format("{0}_{1}", url.Key, Path.GetFileName(url.Value));

                    if (File.Exists(saveDir + fileName))
                    {
                        continue;
                    }

                    using (FileStream fs = File.Create(string.Format("{0}{1}", tempDir, fileName)))
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

                    File.Copy(tempDir + fileName, saveDir + fileName, true);

                }
            }
            catch (Exception)
            {
                //TODO write error log
            }


        }


        private void init()
        {
            this.getConfigurations();
            this.initNotifyIcon();
            this.Hide();
            this.isHidden = true;
            wallpaperUpdating += MainWindow_wallpaperUpdating;
            wallpaperUpdated += MainWindow_wallpaperUpdated;
            updateWallpapersButtonClick(null, null);
            nextWallpaperButtonClick(null, null);
            downloadTimer.Interval = downloadInterval * 3600 * 1000;
            wallpaperTimer.Interval = wallpaperChangeInterval * 3600 * 1000;
            downloadTimer.Elapsed += DownloadTimer_Elapsed;
            wallpaperTimer.Elapsed += WallpaperTimer_Elapsed;
            downloadTimer.Start();
            wallpaperTimer.Start();
        }

        private void MainWindow_wallpaperUpdated()
        {
            notifyIcon.BalloonTipText = "Wallpapers are up to date!";
            notifyIcon.ShowBalloonTip(1000);
            wallpaperIndex = 0;
            setNextWallpaper(isRandom);
        }

        private void MainWindow_wallpaperUpdating()
        {
            notifyIcon.BalloonTipText = "Updating wallpapers...";
            notifyIcon.ShowBalloonTip(1000);
        }

        private void getConfigurations()
        {
            isRandom = bool.Parse(ConfigurationManager.AppSettings["isRandom"]);
            downloadInterval = double.Parse(ConfigurationManager.AppSettings["downloadInterval"]);
            wallpaperChangeInterval = double.Parse(ConfigurationManager.AppSettings["wallpaperChangeInterval"]);
            fetchIndex = int.Parse(ConfigurationManager.AppSettings["fetchIndex"]);
            fetchNumber = int.Parse(ConfigurationManager.AppSettings["fetchNumber"]);
            wallpaperWidth = int.Parse(ConfigurationManager.AppSettings["wallpaperWidth"]);
            wallpaperHeight = int.Parse(ConfigurationManager.AppSettings["wallpaperHeight"]);
        }

        private void initNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.BalloonTipTitle = this.Title;
            notifyIcon.Text = this.Title;
            notifyIcon.Icon = new System.Drawing.Icon(AppDomain.CurrentDomain.BaseDirectory + @"leo_32X32.ico");
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += NotifyIcon_MouseClick;
            notifyIcon.ContextMenu = this.getNotifyIconContextMenu();
        }

        private System.Windows.Forms.ContextMenu getNotifyIconContextMenu()
        {
            System.Windows.Forms.MenuItem updateWallpapersMenu = new System.Windows.Forms.MenuItem("Update Wallpapers");
            System.Windows.Forms.MenuItem nextWallpaperMenu = new System.Windows.Forms.MenuItem("Next Wallpaper");
            System.Windows.Forms.MenuItem optionsMenu = new System.Windows.Forms.MenuItem(
                "Options", 
                new System.Windows.Forms.MenuItem[] { updateWallpapersMenu, nextWallpaperMenu }
                );
            System.Windows.Forms.MenuItem exitMenu = new System.Windows.Forms.MenuItem("Exit");

            updateWallpapersMenu.Click += UpdateWallpapersMenu_Click;
            nextWallpaperMenu.Click += NextWallpaperMenu_Click;
            exitMenu.Click += ExitMenu_Click;

            return new System.Windows.Forms.ContextMenu(
                new System.Windows.Forms.MenuItem[] { optionsMenu, exitMenu}
                );
        }

        private void ExitMenu_Click(object sender, EventArgs e)
        {
            this.isClosing = true;
            this.Close();
        }

        private void NextWallpaperMenu_Click(object sender, EventArgs e)
        {
            nextWallpaperButtonClick(null, null);
        }

        private void UpdateWallpapersMenu_Click(object sender, EventArgs e)
        {
            updateWallpapersButtonClick(null, null);
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (isHidden)
                {
                    this.Show();
                    isHidden = false;
                }
                else
                {
                    this.Hide();
                    isHidden = true;
                }
            }
        }

        private void WallpaperTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            wallpaperTimer.Stop();
            setNextWallpaper(isRandom);
            wallpaperTimer.Start();
        }

        private async void DownloadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            downloadTimer.Stop();
            await getWallpapers();
            downloadTimer.Start();
        }

        private void setNextWallpaper(bool isRandom)
        {
            var imagePathList = Directory.GetFiles(downloadDir).
                OrderByDescending(x=> 
                    {
                        var fileName = Path.GetFileNameWithoutExtension(x);
                        try
                        {
                            var date = int.Parse(fileName.Split('_')[0]);
                            return date;
                        }
                        catch (Exception)
                        {
                            return 0;
                        }
                    }).
                ToList();
            if(isRandom)
            {
                Random rand = new Random();
                wallpaperIndex = rand.Next(imagePathList.Count);
            }
            try
            {
                DesktopWallpaper.SetDesktopBackground(imagePathList[wallpaperIndex], "Stretched");
            }
            catch (Exception)
            {

                //TODO write error log
            }
            wallpaperIndex = (wallpaperIndex + 1) % imagePathList.Count;
        }

        private async Task getWallpapers()
        {
            wallpaperUpdating();
            var imageUrlList = await getPictureUrlList(
                fetchIndex, 
                fetchNumber,
                wallpaperWidth,
                wallpaperHeight
                );
            await downloadImages(imageUrlList, downloadDir, tempDir);
            wallpaperUpdated();
        }

        private void nextWallpaperButtonClick(object sender, RoutedEventArgs e)
        {
            nextWallpaperButton.IsEnabled = false;
            setNextWallpaper(isRandom);
            nextWallpaperButton.IsEnabled = true;
        }

        private async void updateWallpapersButtonClick(object sender, RoutedEventArgs e)
        {
            updateWallpapersButton.IsEnabled = false;
            await getWallpapers();
            updateWallpapersButton.IsEnabled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(!isClosing)
            {
                e.Cancel = true;
                this.Hide();
                isHidden = true;
            }
            else
            {
                this.notifyIcon.Visible = false;
            }
        }
    }
}
