using GoldHunter.Unity.Actors;
using GoldHunter.Unity.Fx;
using GoldHunter.Unity.UI;
using UnityEngine;

namespace GoldHunter.Unity.Managers
{
    /// <summary>
    /// One-component setup.
    ///
    /// Drop this on an empty GameObject in an empty scene and press Play: it
    /// creates the camera, the managers, the spawners and the HUD, wires them
    /// together and starts a match. Everything it builds is an ordinary
    /// component, so you can delete this and lay the scene out by hand once you
    /// want prefabs and art.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldHunterBootstrap : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Height of the top-down camera above the arena floor.")]
        [SerializeField] private float _cameraHeight = 62f;

        [Tooltip("Tilt from straight down. 90 is a pure top-down view.")]
        [SerializeField] private float _cameraPitch = 72f;

        [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.06f, 0.09f);

        [Header("Ground")]
        [SerializeField] private bool _createGroundPlane = true;
        [SerializeField] private Color _groundColor = new Color(0.09f, 0.11f, 0.15f);

        private void Awake()
        {
            EnsureCamera();
            EnsureLight();
            if (_createGroundPlane) EnsureGround();
            EnsureManagers();
        }

        private void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _backgroundColor;
            camera.orthographic = false;
            camera.fieldOfView = 60f;
            camera.farClipPlane = 500f;

            float pitch = Mathf.Clamp(_cameraPitch, 30f, 90f);
            float back = _cameraHeight / Mathf.Tan(pitch * Mathf.Deg2Rad);
            camera.transform.position = new Vector3(0f, _cameraHeight, -back);
            camera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static void EnsureLight()
        {
            if (FindObjectOfType<Light>() != null) return;

            var go = new GameObject("Directional Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }

        private void EnsureGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ArenaFloor";
            ground.transform.SetParent(transform, false);

            // Unity's plane primitive is 10 units across at scale 1.
            ground.transform.localScale = Vector3.one * 8f;
            ground.transform.position = new Vector3(0f, -0.05f, 0f);

            Collider collider = ground.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = _groundColor;
        }

        private void EnsureManagers()
        {
            // MatchManager finds its siblings in Awake, so create them all here first.
            GetOrAdd<StageManager>();
            GetOrAdd<BaseCampManager>();
            GetOrAdd<ShopManager>();
            GetOrAdd<FxDirector>();
            GetOrAdd<PlayerViewSpawner>();
            GetOrAdd<CoinPopperViewSpawner>();
            GetOrAdd<HudController>();
            GetOrAdd<NpcDebugView>();
            GetOrAdd<MatchManager>();
        }

        private T GetOrAdd<T>() where T : Component
        {
            T existing = GetComponent<T>();
            return existing != null ? existing : gameObject.AddComponent<T>();
        }
    }
}
