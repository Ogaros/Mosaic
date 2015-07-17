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

        private int progress;
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

        /// <summary>
        /// <para>Adds all accessible images with their average color to the database.</para>
        /// <para>If successfull, ErrorStatus is NoErrors.</para>
        /// </summary>
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
                    default:
                        {
                            ErrorStatus = FillImageListFromImgur(ref source, webManager); // also sets up source name
                            break;
                        }
                }
            }
            if (ErrorStatus != ErrorType.NoErrors)
                return;
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
            Bitmap bitmap;
            if(source.type == ImageSource.Type.Directory)
            {
                try
                {
                    bitmap = new Bitmap(imagePath);
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
                    bitmap = ImageConverter.BitmapImageToBitmap(tempImage); 
                }
                catch(Exception)
                {
                    ++FailedToIndex;
                    return;
                }                               
            }
            Color averageColor = bitmap.GetAverageColor();
            Image image = new Image(imagePath, averageColor, bitmap.GetSHA256Hash());
            DBManager.AddImage(source, image);
            bitmap.Dispose();
            if (StopIndexing == false)
            {
                Interlocked.Increment(ref progress);
                OnPropertyChanged("Progress");
            }
            semaphore.Release();
        }

        /// <summary>
        /// Returns NoError if successfull.
        /// </summary>
        private ErrorType FillImageListFromImgur(ref ImageSource source, WebManager webManager)
        {
            String jsonGallery = null;
            try
            {
                switch(source.type)
                {
                    case ImageSource.Type.ImgurAlbum:
                        jsonGallery = webManager.GetAlbumJson(source.id);
                        break;
                    default:
                        jsonGallery = webManager.GetGalleryJson(source.id);
                        break;
                }
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
