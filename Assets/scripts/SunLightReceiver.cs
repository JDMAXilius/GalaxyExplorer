// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer
{
    public class SunLightReceiver : MonoBehaviour
    {
        public Transform Sun;
        public bool SetMaterials = true;

        [Tooltip("Write _SunDirection through a MaterialPropertyBlock instead of the shared material. " +
                 "Opt-in, so every prefab authored before this existed behaves exactly as it did.")]
        public bool UsePropertyBlock = false;

        private MeshRenderer currentRenderer;
        private Material moonMaterial = null;
        private MaterialPropertyBlock block;
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");

        private void Awake()
        {
            FindSunIfNeeded();

            currentRenderer = gameObject.GetComponent<MeshRenderer>();
            if (SetMaterials && currentRenderer && currentRenderer.sharedMaterial &&
                currentRenderer.sharedMaterial.name.Equals("Moon"))
            {
                // Somewhere, the Moon's currentRenderer.sharedMaterial is getting
                // changed. Cache the original material here so we can properly
                // clean it up later in OnDestroy.
                moonMaterial = currentRenderer.sharedMaterial;
            }
        }

        public bool FindSunIfNeeded()
        {
            if (!Sun)
            {
                var sunGo = GameObject.Find("Sun");
                if (sunGo)
                {
                    Sun = sunGo.transform;
                    return true;
                }
            }

            return Sun;
        }

        private void LateUpdate()
        {
            if (currentRenderer && FindSunIfNeeded())
            {
                Vector3 dir = (Sun.position - transform.position).normalized;
                WriteSunDirection(new Vector4(dir.x, dir.y, dir.z, 0));
            }
        }

        /// <summary>
        /// One place the direction is written, because there are two ways to write it and only one of them is
        /// safe in the editor.
        ///
        /// The historical path assigns to <c>sharedMaterial</c>, which is the material <i>asset</i> whenever
        /// nothing has instanced it first. In the orbit model a <c>Fader</c> ancestor happens to instance it, but
        /// <c>SolarRowBuilder.Strip</c> removes every <c>Fader</c> from a body's visual, so there the write lands
        /// on the <c>.mat</c> on disk and a play session leaves a sun direction in a diff — the leak CLAUDE.md
        /// warns about having to revert. <see cref="UsePropertyBlock"/> routes the same value through a
        /// <c>MaterialPropertyBlock</c> instead, per renderer, touching no asset. That is also the correct scope:
        /// two renderers sharing one material otherwise fight over a single uniform.
        ///
        /// It is opt-in rather than the default only because flipping it for the twenty-odd receivers already
        /// authored into prefabs is a behaviour change that belongs in its own ticket, not in this one.
        /// </summary>
        private void WriteSunDirection(Vector4 direction)
        {
            if (UsePropertyBlock)
            {
                if (block == null)
                {
                    block = new MaterialPropertyBlock();
                }

                // Read back first: another system may already be writing its own properties on this renderer.
                currentRenderer.GetPropertyBlock(block);
                block.SetVector(SunDirectionId, direction);
                currentRenderer.SetPropertyBlock(block);
                return;
            }

            if (currentRenderer.sharedMaterial)
            {
                currentRenderer.sharedMaterial.SetVector(SunDirectionId, direction);
            }
        }

        private void OnDestroy()
        {
            if (UsePropertyBlock)
            {
                // Nothing on disk to put back; just stop overriding, so the shader's own "no sun set" case
                // (lit all the way round) is what a pooled renderer sees next.
                if (currentRenderer && block != null)
                {
                    currentRenderer.SetPropertyBlock(null);
                }

                return;
            }

            if (currentRenderer && currentRenderer.sharedMaterial.HasProperty("_SunDirection") || moonMaterial)
            {
                if (moonMaterial)
                {
                    moonMaterial.SetVector("_SunDirection", Vector4.zero);
                }
                else
                {
                    currentRenderer.sharedMaterial.SetVector("_SunDirection", Vector4.zero);
                }
            }
        }
    }
}