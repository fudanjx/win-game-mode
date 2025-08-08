using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace GameModeApp
{
    public static class IconGenerator
    {
        // Path for custom icon image
        private static readonly string CustomIconImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom-icon.png");

        public static void GenerateIcons()
        {
            string resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            Directory.CreateDirectory(resourcesPath);

            // Generate app icon (look for custom icon first)
            string iconPath = Path.Combine(resourcesPath, "app.ico");
            if (File.Exists(CustomIconImagePath))
            {
                try
                {
                    GenerateIconFromImage(CustomIconImagePath, iconPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to generate icon from image: {ex.Message}");
                    GenerateAppIcon(iconPath);
                }
            }
            else
            {
                GenerateAppIcon(iconPath);
            }

            GenerateActiveIcon(Path.Combine(resourcesPath, "active.ico"));
            GenerateInactiveIcon(Path.Combine(resourcesPath, "inactive.ico"));
        }

        private static void GenerateAppIcon(string path)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                
                // Draw a simple icon - keyboard with Windows key blocked
                g.FillRectangle(Brushes.DarkGray, 4, 8, 24, 16);

                // Draw Windows key
                g.FillRectangle(Brushes.White, 8, 12, 6, 6);
                g.DrawLine(new Pen(Color.White, 2), 10, 12, 12, 18);
                g.DrawLine(new Pen(Color.White, 2), 8, 15, 14, 15);

                SaveAsIcon(bitmap, path);
            }
        }

        private static void GenerateActiveIcon(string path)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                
                // Draw a colored square representing the active state
                g.FillRectangle(Brushes.Green, 4, 4, 24, 24);
                
                // Add a "Win" symbol with a line through it to indicate blocked
                g.DrawString("Win", new Font("Arial", 10, FontStyle.Bold), Brushes.White, 6, 8);
                g.DrawLine(new Pen(Color.Red, 2), 4, 4, 28, 28);

                SaveAsIcon(bitmap, path);
            }
        }

        private static void GenerateInactiveIcon(string path)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                
                // Draw a colored square representing the inactive state
                g.FillRectangle(Brushes.Gray, 4, 4, 24, 24);
                
                // Add a "Win" symbol
                g.DrawString("Win", new Font("Arial", 10, FontStyle.Bold), Brushes.White, 6, 8);

                SaveAsIcon(bitmap, path);
            }
        }

        private static void SaveAsIcon(Bitmap bitmap, string path)
        {
            // Icon requires a 32bppArgb bitmap
            using (Bitmap bmp = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                }

                // Convert to Icon and save
                IntPtr hIcon = bmp.GetHicon();
                using (Icon icon = Icon.FromHandle(hIcon))
                {
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        icon.Save(fs);
                    }
                }
            }
        }

        private static void GenerateIconFromImage(string imagePath, string iconPath)
        {
            // Load the source image
            using (Bitmap sourceImage = new Bitmap(imagePath))
            {
                // Create a simple 32x32 icon which is standard size
                using (Bitmap iconBitmap = new Bitmap(32, 32))
                using (Graphics g = Graphics.FromImage(iconBitmap))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(sourceImage, 0, 0, 32, 32);
                    
                    // Save as icon
                    IntPtr hIcon = iconBitmap.GetHicon();
                    using (Icon icon = Icon.FromHandle(hIcon))
                    {
                        using (FileStream fs = new FileStream(iconPath, FileMode.Create))
                        {
                            icon.Save(fs);
                        }
                    }
                }
            }
        }
    }
}