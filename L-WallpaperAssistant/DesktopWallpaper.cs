using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace L_WallpaperAssistant
{
    internal class DesktopWallpaper
    {
        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public static void SetDesktopBackground(string imagePath, string desktopBackgroundStyle)
        {
            using (Bitmap bitmap = new Bitmap(imagePath))
            {
                string wallpaperPath = AppDomain.CurrentDomain.BaseDirectory + "wallpaper.bmp";
                bitmap.Save(wallpaperPath, ImageFormat.Bmp);

                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", true);

                switch (desktopBackgroundStyle)
                {
                    case "Stretched":
                        registryKey.SetValue(@"WallpaperStyle", "2");
                        registryKey.SetValue(@"TileWallpaper", "0");
                        break;

                    case "Centered":
                        registryKey.SetValue(@"WallpaperStyle", "0");
                        registryKey.SetValue(@"TileWallpaper", "0");
                        break;

                    case "Tiled":
                        registryKey.SetValue(@"WallpaperStyle", "0");
                        registryKey.SetValue(@"TileWallpaper", "1");
                        break;
                }

                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaperPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
                File.Delete(wallpaperPath);
                Thread.Sleep(500);
            }
        }
    }
}
