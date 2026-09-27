using System;
using System.IO;
using UnityEngine;

namespace Cosmic.Companion
{
    public class BeingKeys : ScriptableObject
    {
        const string Name = "OPENAI_API_KEY";

        [SerializeField] string openAiKey = "";

        // The asset field wins; then the environment; then the project's own gitignored .env, which only exists on a dev machine.
        public string OpenAi
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(openAiKey)) return openAiKey.Trim();
                var env = Environment.GetEnvironmentVariable(Name);
                return !string.IsNullOrWhiteSpace(env) ? env.Trim() : FromDotEnv();
            }
        }

        static string FromDotEnv()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
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
