using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Mosaic
{
    internal static class JsonParser
    {
        public static ImgurGallery DeserializeImgurGallery(String json)
        {                     
            JObject gallery = JObject.Parse(json);
            JToken data = gallery["data"];
            var g = JsonConvert.DeserializeObject<ImgurGallery>(data.ToString());
            g.images_count = g.images.Count; // Sometimes request returns wrong number of images so it's safer to recount
            return g;
        }

        public static Tuple<int, int> GetUserLimitAndClientLimit(String json)
        {
            JObject limits = JObject.Parse(json);
            int userLimit = JsonConvert.DeserializeObject<int>(limits["data"]["UserRemaining"].ToString());
            int clientLimit = JsonConvert.DeserializeObject<int>(limits["data"]["ClientRemaining"].ToString());
            return new Tuple<int, int>(userLimit, clientLimit);
        }

        public static int GetStatusCode(String json)
        {
            JObject response = JObject.Parse(json);
            return JsonConvert.DeserializeObject<int>(response["status"].ToString());
        }
    }
}
