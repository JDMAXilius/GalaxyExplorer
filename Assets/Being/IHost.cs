namespace Cosmic.Companion
{
    /// <summary>What the player has in front of them, told to the being before a turn.</summary>
    public struct Situation
    {
        public string place;
        public string placeId;
        public string layout;
        public string[] held;
        /// <summary>The body the info card describes, "Name (id)", or empty.</summary>
        public string selected;
        /// <summary>A place previewed from a map marker but not yet entered, "Name (id)", or empty.</summary>
        public string previewing;
        /// <summary>Ids of the bodies pull_body can bring out here.</summary>
        public string[] bodies;
        /// <summary>Places that open straight from here (the map's markers), "Name (id)".</summary>
        public string[] destinations;
    }

    public interface IHost
    {
        bool Held { get; }
        Situation Situation();
        void Act(string name, string id);
        void Duck(bool on);
        void Click();
    }
}
