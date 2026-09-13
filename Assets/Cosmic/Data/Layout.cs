using System;
using UnityEngine;

namespace Cosmic
{
    [Serializable]
    public struct Slot
    {
        public Body body;
        public Vector3 localPosition;
        public Vector3 localEuler;

        // Body prefabs are normalised to a 1 m diameter, so this doubles as the body's diameter in metres.
        public float scale;
    }

    [CreateAssetMenu(menuName = "Cosmic/Layout", fileName = "layout")]
    public class Layout : ScriptableObject
    {
        public string id;
        public string title;
        public string subtitle;
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
