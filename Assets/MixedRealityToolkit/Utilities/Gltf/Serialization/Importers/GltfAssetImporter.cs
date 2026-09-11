// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.



namespace Microsoft.MixedReality.Toolkit.Utilities.Gltf.Serialization.Editor
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "gltf")]
    public class GltfAssetImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext context)
        {
            GltfEditorImporter.OnImportGltfAsset(context);
        }
    }
}