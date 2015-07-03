using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic
{
    internal static class JsonParser
    {
        public static ImgurGallery deserializeImgurGallery(String json)
        {                     
            JObject gallery = JObject.Parse(json);
            JToken data = gallery["data"];
            return JsonConvert.DeserializeObject<ImgurGallery>(data.ToString());
        }

        public static Tuple<int, int> getUserLimitAndClientLimit(String json)
        {
            JObject limits = JObject.Parse(json);
            int userLimit = JsonConvert.DeserializeObject<int>(limits["data"]["UserRemaining"].ToString());
            int clientLimit = JsonConvert.DeserializeObject<int>(limits["data"]["ClientRemaining"].ToString());
            return new Tuple<int, int>(userLimit, clientLimit);
        }

        public static int getStatusCode(String json)
        {
            JObject response = JObject.Parse(json);
            return JsonConvert.DeserializeObject<int>(response["status"].ToString());
        }
    }
}
