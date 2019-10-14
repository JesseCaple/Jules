using System;

namespace Jules.PluralKit
{
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CA1707 // Identifiers should not contain underscores
    public class PluralKitMemberModel
    {
        public string id { get; set; }
        public string name { get; set; }
        public string color { get; set; }
        public Uri avatar_url { get; set; }

        public string birthday { get; set; }
        public string pronouns { get; set; }
        public string description { get; set; }
        public string prefix { get; set; }
        public string suffix { get; set; }
        public DateTime created { get; set; }
    }
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore IDE1006 // Naming Styles
}