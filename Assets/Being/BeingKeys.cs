using System;
using System.IO;
using UnityEngine;

namespace Cosmic.Companion
{
    public class BeingKeys : ScriptableObject
    {
        const string Name = "OPENAI_API_KEY";

        [SerializeField] string openAiKey = "";

        // The asset field wins. The environment and the project's gitignored .env are read only in the editor and in
        // development builds (CS-308): a release player never looks for a key beside itself.
        public string OpenAi
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(openAiKey)) return openAiKey.Trim();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var env = Environment.GetEnvironmentVariable(Name);
                return !string.IsNullOrWhiteSpace(env) ? env.Trim() : FromDotEnv();
#else
                return "";
#endif
            }
        }

        static string FromDotEnv()
        {
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && UNITY_STANDALONE)
            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ".env");
            if (!File.Exists(path)) return "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (!line.StartsWith(Name + "=")) continue;
                return line.Substring(Name.Length + 1).Trim().Trim('"', '\'');
            }
#endif
            return "";
        }
    }
}
