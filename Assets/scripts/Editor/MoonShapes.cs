// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// The small moons that are not round (owner's direction, 27 Sep). Pluto's four little moons are rubble a few
    /// tens of kilometres long, far too small for gravity to pull them into balls, so each is the built-in sphere
    /// stretched to its measured axes (New Horizons) and roughened, Kerberos pinched into its two lobes. The
    /// sphere's longitude-latitude UVs survive the deformation, so the maps still wrap. Longest axis is one unit,
    /// the same as the sphere, so <see cref="MoonBuilder"/> and <see cref="SolarMoonBuilder"/> size them as before.
    /// </summary>
    public static class MoonShapes
    {
        private const string Folder = "Assets/models/moon_shapes/";

        private struct Shape
        {
            public Vector3 Axes;    // km, longest first
            public float Lobes;     // 0 for one body; the pinch depth of a contact binary
            public float Rough;     // relief as a share of the radius
            public int Seed;
        }

        private static Shape? For(string id)
        {
            switch (id)
            {
                case "nix": return new Shape { Axes = new Vector3(49.8f, 33.2f, 31.1f), Rough = 0.08f, Seed = 1 };
                case "hydra": return new Shape { Axes = new Vector3(50.9f, 36.1f, 30.9f), Rough = 0.09f, Seed = 2 };
                case "kerberos": return new Shape { Axes = new Vector3(19f, 10f, 9f), Lobes = 0.45f, Rough = 0.07f, Seed = 3 };
                case "styx": return new Shape { Axes = new Vector3(16f, 9f, 8f), Rough = 0.1f, Seed = 4 };
                default: return null;
            }
        }

        /// <summary>The shaped mesh for a moon, written once, or null when the moon is round.</summary>
        public static Mesh Get(string id)
        {
            var shape = For(id);
            if (shape == null)
            {
                return null;
            }

            var path = $"{Folder}{id}_shape.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                return mesh;
            }

            Directory.CreateDirectory(Folder);
            mesh = Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));
            mesh.name = $"{id}_shape";
            Deform(mesh, shape.Value);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void Deform(Mesh mesh, Shape s)
        {
            var scale = s.Axes / s.Axes.x;
            var random = new System.Random(s.Seed);
            var bumps = new Vector4[24];
            for (var i = 0; i < bumps.Length; i++)
            {
                var d = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f).normalized;
                bumps[i] = new Vector4(d.x, d.y, d.z, (float)(random.NextDouble() * 2 - 1));
            }

            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i++)
            {
                var dir = vertices[i].normalized;
                var r = 1f;
                foreach (var b in bumps)
                {
                    var dot = Vector3.Dot(dir, new Vector3(b.x, b.y, b.z));
                    r += s.Rough * b.w * Mathf.Exp((dot - 1f) * 6f);
                }
                if (s.Lobes > 0f)
                {
                    // A waist round the middle of the long axis: two lobes touching.
                    r *= 1f - s.Lobes * Mathf.Exp(-dir.x * dir.x * 18f) * (1f - dir.x * dir.x);
                }
                vertices[i] = Vector3.Scale(dir * r * 0.5f, scale);
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var longest = Mathf.Max(mesh.bounds.size.x, Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] /= longest;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}
