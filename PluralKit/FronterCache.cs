using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Jules.PluralKit
{
    public class FronterCache
    {
        private ConcurrentDictionary<ulong, FronterModel> fronters;

        public FronterCache()
        {
            fronters = new ConcurrentDictionary<ulong, FronterModel>();
        }

        public bool ClearFronter(ulong id)
        {
            return fronters.Remove(id, out _);
        }

        public FronterModel GetFronter(ulong id)
        {
            fronters.TryGetValue(id, out FronterModel fronter);
            return fronter;
        }

        public void SetFronter(ulong id, string nameWithTag, Uri avatarUri)
        {
            fronters[id] = new FronterModel()
            {
                NameWithTag = nameWithTag,
                AvatarUrl = avatarUri == null ? "" : avatarUri.ToString()
            };
        }
    }
}
