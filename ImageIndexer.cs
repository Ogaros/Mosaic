using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;


namespace Mosaic
{
    internal class ImageIndexer : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Progress { get { return progress; } } // number of indexed images        
        public int CurrentImageNumber { get; private set; } // same as progress but increased before image is processed (needed for displaying "X out of imageCount images")
        public int ImageCount { get; private set; }
        public String CurrentImagePath { get; private set; }
        public bool StopIndexing { get; set; }
        public int FailedToIndex { get; private set; } // number of images indexing of which has failed for whatever reason
        public ErrorType ErrorStatus { get; private set; }

        private volatile int progress;
        private String[] imageList;
        private SemaphoreSlim semaphore;
        private int threadCount;

        public ImageIndexer()
        {
            ImageCount = 1; // To make progress bar start empty 
            threadCount = Environment.ProcessorCount;
            semaphore = new SemaphoreSlim(threadCount, threadCount);            
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(disposing)
            {
                if (semaphore != null)
                    semaphore.Dispose();
            }
        }

        public void IndexImages(ImageSource source)
        {
            if (DBManager.ContainsSource(source))
            {
                ErrorStatus = ErrorType.SourceAlreadyIndexed;
                return;
            }
            StopIndexing = false;            
            progress = 0;
            CurrentImageNumber = 0;
            FailedToIndex = 0;
            ErrorStatus = ErrorType.NoErrors;
            using (WebManager webManager = new WebManager())
            {
                switch (source.type)
                {
                    case ImageSource.Type.Directory:
                        {
                            imageList = Directory.GetFiles(source.path, "*.jpg", SearchOption.TopDirectoryOnly);
                            break;
                        }
                    case ImageSource.Type.ImgurAlbum:
                        {
                            ErrorStatus = FillImageListFromImgur(ref source, webManager.GetAlbumJson); // also sets up source name
                            if (ErrorStatus != ErrorType.NoErrors)
                                return;
                            break;
                        }
                    case ImageSource.Type.ImgurGallery:
                        {
                            ErrorStatus = FillImageListFromImgur(ref source, webManager.GetGalleryJson); // also sets up source name
                            if (ErrorStatus != ErrorType.NoErrors)
                                return;
                            break;
                        }
                }
            }
            DBManager.AddSource(source);
            ImageCount = imageList.Length;
            source.imageCount = ImageCount;
            OnPropertyChanged("imageCount");            
            foreach (String imagePath in imageList)
            {                
                Task.Run(() => IndexImage(imagePath, source));                              
            }
            while ((StopIndexing == false && progress < ImageCount) || (StopIndexing == true && semaphore.CurrentCount != threadCount))
            {
                Thread.Sleep(500);
            }
            if (StopIndexing == true)
            {
                DBManager.RemoveSource(source);
                ErrorStatus = ErrorType.IndexingCancelled;
                return;
            }  
            if (FailedToIndex > 0)
                ErrorStatus = ErrorType.PartiallyIndexed;
        }

        private void IndexImage(String imagePath, ImageSource source)
        {
            semaphore.Wait();
            if (StopIndexing == true)
            {
                semaphore.Release();
                return;
            }
            ++CurrentImageNumber;
            OnPropertyChanged("CurrentImageNumber");
            CurrentImagePath = imagePath;
            OnPropertyChanged("CurrentImagePath");
            Bitmap image;
            if(source.type == ImageSource.Type.Directory)
            {
                try
                {
                    image = new Bitmap(imagePath);
                }
                catch (System.IO.FileNotFoundException)
                {
                    ++FailedToIndex;
                    return;
                }
            }
            else
            {
                BitmapImage tempImage = null;
                try
                {
                    tempImage = new BitmapImage(new Uri(imagePath));
                    image = ImageConverter.BitmapImageToBitmap(tempImage); 
                }
                catch(Exception)
                {
                    ++FailedToIndex;
                    return;
                }                               
            }
            Color averageColor = GetAverageColor(image);
            DBManager.AddImage(source, imagePath, averageColor, GetImageHash(image));
            image.Dispose();
            if (StopIndexing == false)
            {
                ++progress;
                OnPropertyChanged("Progress");
            }
            semaphore.Release();
        }

        private ErrorType FillImageListFromImgur(ref ImageSource source, Func<String, String> getGalleryJson)
        {
            String jsonGallery = null;
            try
            {
                jsonGallery = getGalleryJson(source.id);
            }
            catch (System.Net.WebException)
            {
                return ErrorType.CantAccessSource;
            }
            ImgurGallery gallery = JsonParser.DeserializeImgurGallery(jsonGallery);
            source.name = gallery.title;
            imageList = new String[gallery.images_count];
            for (int i = 0; i < gallery.images_count; i++)
            {
                imageList[i] = gallery.images[i].link;
            }
            return ErrorType.NoErrors;
        }

        private static String GetImageHash(Bitmap image)
        {
            using (SHA256 sha = SHA256Managed.Create())
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Jpeg);
                    var hash = sha.ComputeHash(ms.ToArray());
                    return System.Text.Encoding.Default.GetString(hash);
                }
            }
        }

        public static Color GetAverageColor(Bitmap image)
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
