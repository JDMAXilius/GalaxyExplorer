using System.Collections;
using GalaxyExplorer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One tile of the retired desktop planet bar.
///
/// <para><b>Retired, not deleted (roadmap 4.3).</b> The bar is replaced by <c>DesktopDock</c>, which draws the
/// seven places as flat thumbnails in the corner of the screen. This one rendered each body <i>live</i>: a
/// <see cref="Camera"/> per tile, each with its own 256x256 <see cref="RenderTexture"/>, each body moved onto a
/// private <c>PreviewLayer</c> so its camera could see it alone. Eleven tiles is eleven extra cameras and eleven
/// render targets drawn every frame for a widget nothing routes through any more.</para>
///
/// <para>So the tile switches itself off before any of that is built, and the bar costs nothing. The component
/// is left in place because removing it from <c>menu_managers.prefab</c> is prefab surgery that wants a live
/// editor (CS-088); <see cref="keepLegacyBar"/> is here so that surgery can be checked against the old
/// behaviour rather than remembered.</para>
///
/// <para>Note what is <i>not</i> a side effect any more: the layer shuffle below rewrote the layer of every
/// object under a body. Bodies now stay on the layer their prefab was authored with.</para>
/// </summary>
[RequireComponent(typeof(RawImage))]
public class UiWorldPreview : MonoBehaviour
{
    private static int MAX_LAYER_NUMBER = 9;

    [SerializeField] private int targetSlotId;
    [SerializeField] private RawImage image;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI displayNameArea;
    [SerializeField] private PlanetPreviewController planetPreviewController;
    [Tooltip("Shown instead of a live render; for targets that are not always visible (the Moon hides in its orbit).")]
    [SerializeField] private Texture staticPreview;

    [Tooltip("Diagnostic only: bring back the retired live-camera preview tile. The dock replaces it.")]
    [SerializeField] private bool keepLegacyBar;

    private UiPreviewTarget target;
    private Camera targetCamera;
    private RenderTexture renderTexture;

    private static int layerNumber;

    private void OnEnable()
    {
        if (!keepLegacyBar)
        {
            // Before the platform test, before the coroutine, before the camera: the cheapest possible exit.
            // Deactivating from OnEnable is allowed - Unity runs OnDisable straight afterwards, which is why
            // that method has to survive being called with nothing built.
            gameObject.SetActive(false);
            return;
        }

        if (!GalaxyExplorerManager.IsDesktop)
        {
            return;
        }
        displayNameArea.gameObject.SetActive(false);
        image.enabled = false;
        StartCoroutine(WaitForTarget());
    }

    void Initialize()
    {
        button.onClick.AddListener(HandleClick);
        if (staticPreview != null)
        {
            image.texture = staticPreview;
            image.enabled = true;
            displayNameArea.gameObject.SetActive(true);
            displayNameArea.text = target.displayName;
            return;
        }

        var cameraObject = new GameObject("UIViewCamera");
        targetCamera = cameraObject.AddComponent<Camera>();
        renderTexture = new RenderTexture(256,256, 24, RenderTextureFormat.ARGB32);
        renderTexture.antiAliasing = 2;
        targetCamera.targetTexture = renderTexture;
        image.texture = renderTexture;
        displayNameArea.gameObject.SetActive(true);
        image.enabled = true;
        displayNameArea.text = target.displayName;

        var targetLayer = LayerMask.NameToLayer($"PreviewLayer{layerNumber}");
        target.gameObject.SetLayerRecursively(targetLayer);
        targetCamera.cullingMask = 1 << targetLayer;
        layerNumber++;
        if (layerNumber > MAX_LAYER_NUMBER)
        {
            layerNumber = 0;
        }

        targetCamera.transform.SetParent(target.transform);
        PositionCamera();
        targetCamera.clearFlags = CameraClearFlags.Color;
        targetCamera.backgroundColor = Color.clear;
        targetCamera.nearClipPlane = .0001f;
    }

    private void PositionCamera()
    {
        targetCamera.transform.localPosition = target.initialPosition;
        targetCamera.transform.localRotation = target.initialRotation;
        targetCamera.fieldOfView = target.initialFov;
    }

    IEnumerator WaitForTarget()
    {
        var waitForOneSecond = new WaitForSeconds(1);
        target = GetTargetById(targetSlotId);
        while (target == null)
        {
            yield return waitForOneSecond;
            target = GetTargetById(targetSlotId);
        }

        Initialize();
    }

    private UiPreviewTarget GetTargetById(int slotId)
    {
        var previewTargets = FindObjectsByType<UiPreviewTarget>(FindObjectsSortMode.None);
        foreach (var previewTarget in previewTargets)
        {
            if (previewTarget.slotId == slotId)
            {
                return previewTarget;
            }
        }
        return null;
    }

    private void HandleClick()
    {
        if (planetPreviewController != null)
        {
            planetPreviewController.OnButtonSelected(button);
        }

        if (target != null && target.forceSolver != null)
        {
            target.forceSolver.OnPointerDown();
        }
    }

    private void OnDisable()
    {
        if (targetCamera != null)
        {
            Destroy(targetCamera.gameObject);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }
}
