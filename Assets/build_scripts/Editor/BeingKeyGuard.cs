// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;

namespace GalaxyExplorer.Build
{
    /// <summary>Whether the voice guide's key asset would carry a key into a player (CS-253, CS-299).</summary>
    public static class BeingKeyGuard
    {
        public const string KeysAsset = "Assets/Being/Data/being_keys.asset";

        public static bool ShipsKey()
        {
            var keys = AssetDatabase.LoadMainAssetAtPath(KeysAsset);
            if (keys == null)
            {
                return false;
            }

            // By serialized name: the field is private. A renamed field counts as a key, so this can never pass by
            // no longer looking.
            var key = new SerializedObject(keys).FindProperty("openAiKey");
            return key == null || !string.IsNullOrWhiteSpace(key.stringValue);
        }
    }
}
