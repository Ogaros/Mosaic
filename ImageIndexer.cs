using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.ComponentModel;

namespace Mosaic
{
    class ImageIndexer : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int progress { get; set; }
        private String[] imageList;
        private String dirPath;
               

        public int countImages(String directoryPath)
        {
            if (dirPath != directoryPath)
            {
                dirPath = directoryPath;
                imageList = Directory.GetFiles(directoryPath, "*.jpg", SearchOption.TopDirectoryOnly);
            }
            return imageList.Length;
        }

        public void indexImages(Action<int, String> callback)
        {
            if (DBManager.directoryRecorded(dirPath))
                return;
            progress = 0;
            DBManager.addDirectory(dirPath);
            foreach(String imagePath in imageList)
            {
                ++progress;
                
                callback(progress, imagePath);
                indexImage(imagePath);                
                OnPropertyChanged("progress");
            }            
        }

        private void indexImage(String imagePath)
        {
            Bitmap image = new Bitmap(imagePath);
            Color averageColor = getAverageColor(image);            
            DBManager.addImage(imagePath.Substring(imagePath.LastIndexOf('\\')), averageColor, getImageHash(imagePath));
        }

        private String getImageHash(String imagePath)
        {
            SHA256 sha = SHA256Managed.Create();
            using(FileStream fstream = File.OpenRead(imagePath))
            {
                return System.Text.Encoding.Default.GetString(sha.ComputeHash(fstream));
            }
        }

        public static Color getAverageColor(Bitmap image)
        {
            long R = 0, G = 0, B = 0;
            Color tempColor;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    tempColor = image.GetPixel(x, y);
                    R += tempColor.R;
                    G += tempColor.G;
                    B += tempColor.B;
                }
            }

            long pixelCount = image.Height * image.Width;
            return Color.FromArgb((int)(R / pixelCount), (int)(G / pixelCount), (int)(B / pixelCount));
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
