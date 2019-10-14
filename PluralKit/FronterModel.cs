using System;
using System.Collections.Generic;
using System.Text;

namespace Jules.PluralKit
{
#pragma warning disable CA1056 // Uri properties should not be strings
    public class FronterModel
    {
        public string NameWithTag { get; set; }
        public string AvatarUrl { get; set; }
    }
#pragma warning restore CA1056 // Uri properties should not be strings
}
