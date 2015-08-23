using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Mosaic.Indexer
{    
    class ImageList : IEnumerable<Image>
    {
        private List<Image> _list;
        private String[] _imageFormats = { "*.jpg", "*.png", "*.bmp", "*.tiff" };

        public int Count { get { return _list.Count; } }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return this.GetEnumerator(); }
        public IEnumerator<Image> GetEnumerator() { return _list.GetEnumerator(); }

        public ImageList()
        {
            _list = new List<Image>();
        }

        public void Clear()
        {
            _list.Clear();
        }
        
        public ErrorType Fill(ImageSource source)
        {            
            ErrorType errorStatus = ErrorType.NoErrors;
            switch (source.type)
            {
                case ImageSource.Type.Directory:
                    {
                        FillFromDirectory(source);
                        break;
                    }
                case ImageSource.Type.ImgurAlbum:
                case ImageSource.Type.ImgurGallery:
                    {
                        errorStatus = FillFromImgur(source); // also sets up source name                        
                        break;
                    }
            }
            return errorStatus;            
        }

        private void FillFromDirectory(ImageSource source)
        {
            foreach (String imageFormat in _imageFormats)
                foreach (String path in Directory.GetFiles(source.path, imageFormat, SearchOption.TopDirectoryOnly))
                    _list.Add(new Image(path, default(Color), null));
        }

        private ErrorType FillFromImgur(ImageSource source)
        {
            String jsonGallery = null;
            using (WebManager webManager = new WebManager())
            {
                try
                {
                    switch (source.type)
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
                for (int i = 0; i < gallery.images_count; i++)
                {
                    _list.Add(new Image(gallery.images[i].link, default(Color), null));
                }
            }
            return ErrorType.NoErrors;
        }    
    }
}
