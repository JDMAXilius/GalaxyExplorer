namespace Cosmic.Companion
{
    public struct Situation
    {
        public string place;
        public string layout;
        public string[] held;
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
