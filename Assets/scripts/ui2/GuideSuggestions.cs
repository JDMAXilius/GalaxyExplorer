// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Companion = Cosmic.Companion;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The guide's suggestion bubbles, made from what the player has in front of them: the body on the card first
    /// (questions about it, and the things the guide can do with it), else the place (its questions, the places
    /// its map opens, the bodies it can pull). Every line is something the guide can answer or carry out with its
    /// tools. Lines shown recently are skipped, so asking again brings different ones.
    /// </summary>
    public static class GuideSuggestions
    {
        // Questions any body can take; {0} is its name.
        private static readonly string[] BodyQuestions =
        {
            "What is {0} made of?",
            "How big is {0} compared to Earth?",
            "How long is a day on {0}?",
            "Tell me something surprising about {0}.",
            "How did {0} form?",
            "Could people ever visit {0}?",
        };

        private static readonly string[] PlanetQuestions =
        {
            "How long is a year on {0}?",
            "What is the weather like on {0}?",
            "How far is {0} from the Sun?",
        };

        private static readonly string[] MoonQuestions =
        {
            "What is {0} like up close?",
            "How big is {0} next to its planet?",
            "Could {0} have water?",
        };

        private static readonly Dictionary<string, string[]> Specific = new Dictionary<string, string[]>
        {
            ["sun"] = new[] { "How hot is the Sun?", "How long will the Sun keep shining?", "What is a solar flare?", "How old is the Sun?" },
            ["mercury"] = new[] { "Why does Mercury have no air?", "How hot and cold does Mercury get?" },
            ["venus"] = new[] { "Why is Venus so hot?", "Why does Venus spin backwards?" },
            ["earth"] = new[] { "Why is Earth the only planet we know with life?", "What would Earth be like without the Moon?", "Why is the sky blue?" },
            ["mars"] = new[] { "Why is Mars red?", "Was there ever water on Mars?", "How tall is Olympus Mons?" },
            ["jupiter"] = new[] { "What is the Great Red Spot?", "Does Jupiter have a surface?", "How many moons does Jupiter have?" },
            ["saturn"] = new[] { "Why does Saturn have rings?", "Could Saturn float in water?", "What are the rings made of?" },
            ["uranus"] = new[] { "Why does Uranus spin on its side?", "Why is Uranus blue-green?" },
            ["neptune"] = new[] { "Why is Neptune so windy?", "How was Neptune discovered?" },
            ["pluto"] = new[] { "Why is Pluto not a planet any more?", "What did New Horizons find on Pluto?" },
            ["moon"] = new[] { "How did the Moon form?", "Why do we only see one side of the Moon?" },
            ["europa"] = new[] { "Could anything live in Europa's ocean?" },
            ["titan"] = new[] { "What are Titan's lakes made of?" },
            ["io"] = new[] { "Why does Io have so many volcanoes?" },
        };

        private static readonly Dictionary<string, string[]> PlaceQuestions = new Dictionary<string, string[]>
        {
            ["milky_way"] = new[] { "How big is the Milky Way?", "Where is the Sun in our galaxy?", "How many stars are in the Milky Way?", "What is at the centre of our galaxy?", "What happens when we meet Andromeda?" },
            ["cosmic_web"] = new[] { "What is the Cosmic Web?", "What is dark matter?", "What are the empty voids?", "How big is the Cosmic Web?" },
            ["galaxies"] = new[] { "How many galaxies are there?", "Why are galaxies different shapes?", "How far back in time are we seeing?" },
            ["andromeda"] = new[] { "How far away is Andromeda?", "When will Andromeda hit the Milky Way?", "Can I see Andromeda from Earth?" },
            ["whirlpool"] = new[] { "Why does the Whirlpool have a companion?", "How far away is the Whirlpool?" },
            ["pinwheel"] = new[] { "Why is the Pinwheel lopsided?", "How big is the Pinwheel Galaxy?" },
            ["triangulum"] = new[] { "Is Triangulum part of our group?", "How far away is Triangulum?" },
            ["sagittarius_a"] = new[] { "How heavy is the black hole at the centre?", "Can anything escape a black hole?", "How did we photograph it?" },
            ["solar_system"] = new[] { "How old is the Solar System?", "Why do planets orbit the Sun?", "Which planet is the biggest?", "Why are the outer planets so big?" },
            ["solar_system_planets"] = new[] { "Which planet is the biggest?", "Which planet is the hottest?", "How much bigger is the Sun than Earth?", "Which planet has the most moons?" },
            ["hd110067"] = new[] { "Why do these six planets move in step?", "Could these worlds have surfaces?", "How far away is HD 110067?" },
            ["orion"] = new[] { "How are stars born in Orion?", "Can I see the Orion Nebula from Earth?" },
            ["crab"] = new[] { "What exploded to make the Crab Nebula?", "What is spinning at its centre?" },
            ["helix"] = new[] { "Will the Sun become a nebula like this?", "What is a planetary nebula?" },
            ["pillars"] = new[] { "What are the Pillars of Creation made of?", "Are new stars forming inside them?" },
            ["homunculus"] = new[] { "What happened to Eta Carinae?", "Will this star explode?" },
            ["ngc1501"] = new[] { "Why is NGC 1501 called the Oyster Nebula?", "What is the star in the middle?" },
            ["trumpler14"] = new[] { "Why is Trumpler 14 so bright?", "How young are these stars?" },
        };

        private static readonly string[] Anywhere =
        {
            "What am I looking at?",
            "What should I see next?",
            "Tell me something surprising here.",
        };

        // The dock's places, as the player would ask for them: place id, then the line.
        private static readonly string[,] Places =
        {
            { "cosmic_web", "Take me to the Cosmic Web." },
            { "galaxies", "Show me the galaxies." },
            { "milky_way", "Take me to the Milky Way." },
            { "andromeda", "Take me to Andromeda." },
            { "solar_system", "Open the Solar System." },
            { "sagittarius_a", "Take me to the Galactic Center." },
        };

        private static readonly List<string> Recent = new List<string>();
        private const int RecentLength = 12;
        private static readonly System.Random Dice = new System.Random();

        /// <summary>
        /// Up to <paramref name="count"/> lines for the situation: questions and things to do, mixed and in no fixed
        /// order, none of them shown in the last few rounds unless the pool has run dry.
        /// </summary>
        public static List<string> For(Companion.Situation s, BodyInfo selected, bool selectedOut, bool hasSound, int count)
        {
            var questions = new List<string>();
            var actions = new List<string>();

            if (selected != null)
            {
                var name = Mid(selected.DisplayName);
                if (Specific.TryGetValue(selected.Id, out var own))
                {
                    questions.AddRange(own);
                }

                foreach (var q in BodyQuestions) questions.Add(string.Format(q, name));
                foreach (var q in selected.IsMoon ? MoonQuestions : selected.Id == "sun" ? new string[0] : PlanetQuestions)
                {
                    questions.Add(string.Format(q, name));
                }

                if (hasSound) actions.Add($"Play {Possessive(name)} sound.");
                if (selectedOut) actions.Add($"Put {name} back.");
                if (!selected.IsMoon && selected.Moons != null && selected.Moons.Length > 0)
                {
                    // A moon can be pulled once its planet is out; the guide pulls the planet first when it is not.
                    foreach (var moon in selected.Moons)
                    {
                        if (moon != null) actions.Add($"Show me {Mid(moon.DisplayName)}.");
                    }
                }
                if (selected.IsMoon && selected.Orbits != null) actions.Add($"Tell me about {Mid(selected.Orbits.DisplayName)}.");
            }
            else
            {
                if (!string.IsNullOrEmpty(s.placeId) && PlaceQuestions.TryGetValue(s.placeId, out var own))
                {
                    questions.AddRange(own);
                }
                questions.AddRange(Anywhere);
                if (!string.IsNullOrEmpty(s.previewing))
                {
                    var preview = NameOf(s.previewing);
                    actions.Add($"Take me into {preview}.");
                    questions.Insert(0, $"What is {preview}?");
                }
            }

            // With a body on the card, the things to do are about that body; the place's own come when nothing is.
            if (s.destinations != null && selected == null)
            {
                foreach (var d in s.destinations) actions.Add($"Take me to {Article(NameOf(d))}.");
            }

            if (s.bodies != null && selected == null)
            {
                foreach (var b in s.bodies) actions.Add($"Show me {Title(b)}.");
            }

            if (s.held != null && s.held.Length > 1) actions.Add("Put everything back.");
            if (selected == null && s.placeId == "solar_system") actions.Add("Show me the planets in a row.");
            if (selected == null && s.placeId == "solar_system_planets") actions.Add("Show me the orbits.");
            for (var i = 0; selected == null && i < Places.GetLength(0); i++)
            {
                if (Places[i, 0] != s.placeId) actions.Add(Places[i, 1]);
            }

            // Questions lead; at least one thing to do; the rest is whichever pool is richer.
            var result = new List<string>();
            Take(questions, 1, result);
            Take(actions, 1, result);
            while (result.Count < count && (questions.Count > 0 || actions.Count > 0))
            {
                Take(Dice.Next(2) == 0 && questions.Count > 0 || actions.Count == 0 ? questions : actions, 1, result);
            }

            Shuffle(result);
            foreach (var line in result)
            {
                Recent.Remove(line);
                Recent.Add(line);
            }
            while (Recent.Count > RecentLength) Recent.RemoveAt(0);
            return result;
        }

        /// <summary>True for a line that has the guide do something (travel, pull, play, put back) rather than answer.</summary>
        public static bool IsAction(string line)
        {
            foreach (var verb in ActionVerbs)
            {
                if (line.StartsWith(verb, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static readonly string[] ActionVerbs = { "Play ", "Show me ", "Put ", "Take me ", "Open " };

        // A fresh line from the pool if there is one, else the oldest of the recent ones.
        private static void Take(List<string> pool, int n, List<string> into)
        {
            for (var k = 0; k < n && pool.Count > 0; k++)
            {
                var fresh = pool.FindAll(p => !Recent.Contains(p) && !into.Contains(p));
                var from = fresh.Count > 0 ? fresh : pool.FindAll(p => !into.Contains(p));
                if (from.Count == 0) return;
                var pick = from[Dice.Next(from.Count)];
                into.Add(pick);
                pool.Remove(pick);
            }
        }

        private static void Shuffle(List<string> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Dice.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // "Orion Nebula (orion)" -> "Orion Nebula".
        private static string NameOf(string entry)
        {
            var cut = entry.LastIndexOf(" (", System.StringComparison.Ordinal);
            return cut > 0 ? entry.Substring(0, cut) : entry;
        }

        private static string Title(string id) => id == "sun" ? "the Sun" : id == "moon" ? "the Moon" : char.ToUpperInvariant(id[0]) + id.Substring(1);

        // "The Sun" reads "the Sun" inside a sentence.
        private static string Mid(string name) => name == "Moon" ? "the Moon" : name != null && name.StartsWith("The ") ? "the " + name.Substring(4) : name;

        // "Crab Nebula" reads "the Crab Nebula"; a galaxy or a star cluster's catalogue name does not take one.
        private static string Article(string name) =>
            name.EndsWith("Nebula") || name.StartsWith("Pillars") || name.StartsWith("Galactic") || name == "Solar System" ? "the " + name : name;

        private static string Possessive(string name) => name.EndsWith("s") ? name + "'" : name + "'s";
    }
}
