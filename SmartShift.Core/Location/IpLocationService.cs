using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SmartShift.Core.Location
{
    public class IpLocationResult
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string City { get; set; }
        public string Timezone { get; set; }
    }

    public static class IpLocationService
    {
        private const string ApiUrl = "http://ip-api.com/json/?fields=status,message,lat,lon,city,timezone";

        public static async Task<IpLocationResult> DetectLocationAsync(int timeoutMs = 10000)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(ApiUrl);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;
                request.UserAgent = "SmartShift/1.0";

                using (var response = (HttpWebResponse)await Task.Factory.FromAsync(
                    request.BeginGetResponse, request.EndGetResponse, null))
                using (var stream = response.GetResponseStream())
                using (var reader = new System.IO.StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync();
                    var data = JsonConvert.DeserializeObject<IpApiResponse>(json);

                    if (data.Status != "success")
                    {
                        Logger.Warning($"IP 定位 API 返回失败: {data.Message}");
                        throw new Exception($"定位失败: {data.Message}");
                    }

                    return new IpLocationResult
                    {
                        Latitude = data.Lat,
                        Longitude = data.Lon,
                        City = data.City,
                        Timezone = data.Timezone
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"IP 定位异常: {ex.Message}");
                throw;
            }
        }

        private class IpApiResponse
        {
            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("lat")]
            public double Lat { get; set; }

            [JsonProperty("lon")]
            public double Lon { get; set; }

            [JsonProperty("city")]
            public string City { get; set; }

            [JsonProperty("timezone")]
            public string Timezone { get; set; }
        }
    }
}
