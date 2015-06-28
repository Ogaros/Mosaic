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
using System.Windows.Media.Imaging;


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
        public int failedToIndex { get; private set; }
        public ErrorType errorStatus { get; private set; }
        private String[] imageList;

        public ImageIndexer()
        {
            imageCount = 1; // To make progress bar start empty 
        }

        public void indexImages(ImageSource source)
        {
            if (DBManager.containsSource(source))
            {
                errorStatus = ErrorType.SourceAlreadyIndexed;
                return;
            }
            stopIndexing = false;            
            progress = 0;
            currentImageNumber = 0;
            failedToIndex = 0;
            errorStatus = ErrorType.NoErrors;            
            WebManager webManager = new WebManager();
            switch(source.type)
            {
                case ImageSource.Type.Directory:
                    {
                        imageList = Directory.GetFiles(source.path, "*.jpg", SearchOption.TopDirectoryOnly);                        
                        break;
                    }
                case ImageSource.Type.ImgurAlbum:
                    {
                        String jsonAlbum = null;
                        try
                        {
                            jsonAlbum = webManager.getAlbumJson(source.id);
                        }
                        catch (System.Net.WebException)
                        {
                            errorStatus = ErrorType.CantAccessSource;
                            return;
                        }
                        ImgurGallery gallery = JsonParser.deserializeImgurGallery(jsonAlbum);
                        source.name = gallery.title;
                        imageList = new String[gallery.images_count];
                        for (int i = 0; i < gallery.images_count; i++)
                        {
                            imageList[i] = gallery.images[i].link;
                        }
                        break;
                    }
                case ImageSource.Type.ImgurGallery:
                    {
                        String jsonGallery = null;
                        try
                        {
                            jsonGallery = webManager.getGalleryJson(source.id);
                        }
                        catch(System.Net.WebException)
                        {
                            errorStatus = ErrorType.CantAccessSource;
                            return;
                        }
                        ImgurGallery gallery = JsonParser.deserializeImgurGallery(jsonGallery);
                        source.name = gallery.title;
                        imageList = new String[gallery.images_count];
                        for (int i = 0; i < gallery.images_count; i++ )
                        {
                            imageList[i] = gallery.images[i].link;
                        }
                        break;
                    }
            }
            DBManager.addSource(source);
            imageCount = imageList.Length;
            source.imageCount = imageCount;
            OnPropertyChanged("imageCount");
            foreach (String imagePath in imageList)
            {
                ++currentImageNumber;
                OnPropertyChanged("currentImageNumber");
                currentImagePath = imagePath;
                OnPropertyChanged("currentImagePath");
                indexImage(imagePath, source);
                if (stopIndexing == true)
                {
                    DBManager.removeSource(source);
                    errorStatus = ErrorType.IndexingCancelled;
                    return;
                }
                ++progress;
                OnPropertyChanged("progress");
            }
            if (failedToIndex > 0)
                errorStatus = ErrorType.PartiallyIndexed;
        }

        private void indexImage(String imagePath, ImageSource source)
        {
            Bitmap image;
            if(source.type == ImageSource.Type.Directory)
            {
                try
                {
                    image = new Bitmap(imagePath);
                }
                catch (System.IO.FileNotFoundException)
                {
                    ++failedToIndex;
                    return;
                }
            }
            else
            {
                BitmapImage tempImage = null;
                try
                {
                    tempImage = new BitmapImage(new Uri(imagePath));
                }
                catch(System.IO.FileNotFoundException)
                {
                    ++failedToIndex;
                    return;
                }
                image = ImageConverter.BitmapImageToBitmap(tempImage);
            }
            Color averageColor = getAverageColor(image);
            DBManager.addImage(source, imagePath, averageColor, getImageHash(image));
            image.Dispose();
        }

        private String getImageHash(Bitmap image)
        {
            SHA256 sha = SHA256Managed.Create();
            using(MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                var hash = sha.ComputeHash(ms.ToArray());
                return System.Text.Encoding.Default.GetString(hash);
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
