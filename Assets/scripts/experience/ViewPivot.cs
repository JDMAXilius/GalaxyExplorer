// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>The two transforms under the desktop camera that Home puts back to their first pose.</summary>
    public class ViewPivot : MonoBehaviour
    {
        [SerializeField] private Transform pivot = null;
        [SerializeField] private Transform entityToMove = null;

        public Transform Pivot => pivot;
        public Transform EntityToMove => entityToMove;
    }
}
