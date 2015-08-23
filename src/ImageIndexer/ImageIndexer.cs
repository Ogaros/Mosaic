using Mosaic.Indexer;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;


namespace Mosaic
{
    internal class ImageIndexer : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Progress { get { return _progress; } } // number of indexed images        
        public int CurrentImageNumber { get; private set; } // same as progress but increased before image is processed (needed for displaying "X out of imageCount images")
        public int ImageCount { get; private set; }
        public String CurrentImagePath { get; private set; }
        public bool StopIndexing { get; set; }
        public int FailedToIndex { get; private set; } // number of images indexing of which has failed for whatever reason
        public ErrorType ErrorStatus { get; private set; }

        private int _progress;
        private ImageList _imageList;
        private SemaphoreSlim _semaphore;
        private int _threadCount;

        public ImageIndexer()
        {
            ImageCount = 1; // To make progress bar start empty 
            _threadCount = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_threadCount, _threadCount);
            _imageList = new ImageList();
        }

        public void Dispose()
        {
            if (_semaphore != null)
                _semaphore.Dispose();
        }

        private void SetupStartingValues()
        {
            StopIndexing = false;
            _progress = 0;
            CurrentImageNumber = 0;
            FailedToIndex = 0;
            ErrorStatus = ErrorType.NoErrors;
            _imageList.Clear();
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

            SetupStartingValues();
            
            ErrorStatus = _imageList.Fill(source);
            if (ErrorStatus != ErrorType.NoErrors)
                return;

            DBManager.AddSource(source);

            ImageCount = _imageList.Count;
            source.imageCount = ImageCount;
            OnPropertyChanged("imageCount"); 
           
            foreach (Image image in _imageList)
            {
                if (StopIndexing == true)
                    break;
                _semaphore.Wait();                
                Task.Run(() => IndexImage(image, source));                              
            }

            while ((StopIndexing == false && _progress < ImageCount) || (StopIndexing == true && _semaphore.CurrentCount != _threadCount))
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

        private void IndexImage(Image image, ImageSource source)
        {            
            if (StopIndexing == true)
            {
                _semaphore.Release();
                return;
            }

            ++CurrentImageNumber;
            OnPropertyChanged("CurrentImageNumber");

            CurrentImagePath = image.path;
            OnPropertyChanged("CurrentImagePath");

            using (Bitmap bitmap = ImageLoader.LoadImageBitmap(image, source.type))
            {
                if(bitmap == null)
                {
                    ++FailedToIndex;
                    return;
                }
                image.color = bitmap.GetAverageColor();
                image.hashcode = bitmap.GetSHA256Hash();
                DBManager.AddImage(source, image);
            }           
 
            if (StopIndexing == false)
            {
                Interlocked.Increment(ref _progress);
                OnPropertyChanged("Progress");
            }

            _semaphore.Release();
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
