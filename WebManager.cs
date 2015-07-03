using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;


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

        public String getGalleryJson(String galleryID)
        {
            String url = "https://api.imgur.com/3/gallery/" + galleryID;
            String jsonGallery = client.DownloadString(url);
            int responseStatusCode = JsonParser.getStatusCode(jsonGallery);
            if (responseStatusCode != 200)
                throw new WebException("Imgur returned error code: " + responseStatusCode.ToString());
            int[] arr = new int[10];
            arr.Where(x => x % 2 == 0).Sum();
            return jsonGallery;           
        }
        
        public String getAlbumJson(String alumID)
        {
            String url = "https://api.imgur.com/3/album/" + alumID;
            String jsonAlbum = client.DownloadString(url);
            int responseStatusCode = JsonParser.getStatusCode(jsonAlbum);
            if (responseStatusCode != 200)
                throw new WebException("Imgur returned error code: " + responseStatusCode.ToString());
            return jsonAlbum;
        }

        public String getLimitsJson()
        {
            String url = "https://api.imgur.com/3/credits";
            return client.DownloadString(url);
        }

        public void Dispose()
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
                GC.SuppressFinalize(this);
            }
        }
    }
}
