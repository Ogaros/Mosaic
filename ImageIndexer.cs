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
        public int progress { get; private set; }
        public int currentImageNumber { get; private set; } // same as progress but increased before image is processed (needed for displaying "X out of imageCount images")
        public int imageCount { get; private set; }
        public String currentImagePath { get; private set; }
        public bool stopIndexing = false;
        private String[] imageList;

        public ImageIndexer()
        {
            imageCount = 10; // To make progress bar start empty 
        }

        public int indexImages(ImageSource source)
        {
            if (DBManager.containsSource(source))
                return 1;
            stopIndexing = false;            
            progress = 0;
            currentImageNumber = 0;
            DBManager.addSource(source);
            if (source.type == ImageSource.Type.Directory)
            {
                imageList = Directory.GetFiles(source.path, "*.jpg", SearchOption.TopDirectoryOnly);
                imageCount = imageList.Length;
                source.imageCount = imageCount;
                OnPropertyChanged("imageCount");
            }
            foreach (String imagePath in imageList)
            {
                ++currentImageNumber;
                OnPropertyChanged("currentImageNumber");
                currentImagePath = imagePath;
                OnPropertyChanged("currentImagePath");
                if (indexImage(imagePath, source) == false || stopIndexing)
                    return 2;
                ++progress;
                OnPropertyChanged("progress");
            }
            return 0;
        }

        private bool indexImage(String imagePath, ImageSource source)
        {
            if (stopIndexing)
                return false;
            Bitmap image = new Bitmap(imagePath);
            Color averageColor = getAverageColor(image);
            DBManager.addImage(source, imagePath, averageColor, getImageHash(imagePath));
            image.Dispose();
            return true;
        }

        private String getImageHash(String imagePath)
        {
            SHA256 sha = SHA256Managed.Create();
            using (FileStream fstream = File.OpenRead(imagePath))
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
