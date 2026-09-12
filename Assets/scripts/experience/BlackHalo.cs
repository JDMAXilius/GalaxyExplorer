// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// A soft black disc that sits behind a faint object, always facing the player, so a nebula reads against a
    /// lit room instead of washing out. Created by <see cref="EnvironmentController.SpawnHalo"/>.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class BlackHalo : MonoBehaviour
    {
        private const float SizeFactor = 1.6f;   // the reference halo is about 1.6x the subject
        private const float BehindMetres = 0.15f;
        private const float FadeSeconds = 0.4f;

        private Transform _target;
        private Transform _quad;
        private Renderer _renderer;
        private Material _material;
        private Camera _camera;
        private float _diameter;
        private float _alpha;
        private float _alphaTarget;

        internal void Initialise(Transform target, float diameter, Material material)
        {
            _target = target;
            _diameter = diameter;
            _material = new Material(material);
            _renderer = BuildQuad();
            _quad = _renderer.transform;
            _camera = Camera.main;
            SetAlpha(0f);
        }

        private Renderer BuildQuad()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "halo_quad";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private void OnDestroy()
        {
            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Forget(this);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        public void SetVisible(bool visible) => _alphaTarget = visible ? 1f : 0f;

        /// <summary>Call when the subject's size changes, e.g. after the player scales it.</summary>
        public void SetDiameter(float diameter) => _diameter = diameter;

        private void LateUpdate()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            if (!Mathf.Approximately(_alpha, _alphaTarget))
            {
                SetAlpha(Mathf.MoveTowards(_alpha, _alphaTarget, Time.deltaTime / FadeSeconds));
            }

            if (_alpha <= 0.001f)
            {
                return;
            }

            // Sit behind the subject along the view ray, facing the player, scaled to cover it with margin.
            var toCamera = _camera.transform.position - _target.position;
            var distance = toCamera.magnitude;
            var direction = distance > 0.0001f ? toCamera / distance : Vector3.back;
            transform.position = _target.position - direction * BehindMetres;
            transform.rotation = Quaternion.LookRotation(-direction, _camera.transform.up);
            var size = _diameter * SizeFactor;
            _quad.localScale = new Vector3(size, size, 1f);
        }

        private void SetAlpha(float a)
        {
            _alpha = a;
            _renderer.enabled = a > 0.001f;
            var c = _material.color;
            _material.color = new Color(c.r, c.g, c.b, a);
        }
    }
}
