using System;
using System.Linq;
using System.Net;


namespace Mosaic
{    
    internal class WebManager : IDisposable
    {
        private const String imgurClientID = "321a023fb8a74a2";
        private WebClient client = new WebClient();

        public WebManager()
        {
            client.Headers.Add("Authorization", "Client-ID " + imgurClientID);
        }

        public String GetGalleryJson(String galleryID)
        {
            String url = "https://api.imgur.com/3/gallery/" + galleryID;
            String jsonGallery = client.DownloadString(url);
            int responseStatusCode = JsonParser.GetStatusCode(jsonGallery);
            if (responseStatusCode != 200)
                throw new WebException("Imgur returned error code: " + responseStatusCode.ToString());
            return jsonGallery;           
        }
        
        public String GetAlbumJson(String alumID)
        {
            String url = "https://api.imgur.com/3/album/" + alumID;
            String jsonAlbum = client.DownloadString(url);
            int responseStatusCode = JsonParser.GetStatusCode(jsonAlbum);
            if (responseStatusCode != 200)
                throw new WebException("Imgur returned error code: " + responseStatusCode.ToString());
            return jsonAlbum;
        }

        public String GetLimitsJson()
        {
            String url = "https://api.imgur.com/3/credits";
            return client.DownloadString(url);
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
                if(client != null)
                    client.Dispose();
            }
        }
    }
}
