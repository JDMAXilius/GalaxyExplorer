using System;
using UnityEngine;

namespace Cosmic
{
    public enum LayoutKind { Row, Relative, Schematic, Realistic }

    [Serializable]
    public struct Slot
    {
        public Body body;
        public Vector3 localPosition;
        public Vector3 localEuler;

        public float scale;

        // Rings included, as a multiple of the diameter; zero reads as 1 because a struct field cannot carry an initialiser here.
        public float spanRatio;
    }

    [CreateAssetMenu(menuName = "Cosmic/Layout", fileName = "layout")]
    public class Layout : ScriptableObject
    {
        public string id;
        public string title;
        public string subtitle;
        public LayoutKind kind = LayoutKind.Row;
        public float transitionSeconds = 0.8f;
        public Slot[] slots = Array.Empty<Slot>();

        public bool TryFind(Body body, out Slot slot)
        {
            if (body != null && slots != null)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i].body == body)
                    {
                        slot = slots[i];
                        return true;
                    }
                }
            }

            slot = default;
            return false;
        }
    }
}
