using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Jules.PluralKit
{
    public class PluralKitAPIService
    {
        public PluralKitAPIService() { }

        public async Task<PluralKitSystemModel> GetSystem(ulong userId)
        {
            var uri = new Uri($"https://api.pluralkit.me/v1/a/{userId}");
            return await GetJson<PluralKitSystemModel>(uri);
        }

        public async Task<List<PluralKitMemberModel>> GetMembers(string pkSystemId)
        {
            var uri = new Uri($"https://api.pluralkit.me/v1/s/{pkSystemId}/members");
            return await GetJson<List<PluralKitMemberModel>>(uri);
        }

        private async Task<T> GetJson<T>(Uri uri)
        {
            try
            {
                var request = WebRequest.CreateHttp(uri);
                request.ContentType = "application/json; charset=utf-8";
                using (var response = request.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var text = await reader.ReadToEndAsync();
                        return JsonConvert.DeserializeObject<T>(text);
                    }
                }
            }
            catch (WebException)
            {
                return default(T);
            }
        }
    }
}
