using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The row the retired desktop planet bar's tiles sat in: which tile is selected, and the light that swung
/// round to key the previews.
///
/// <para><b>Retired, not deleted (roadmap 4.3).</b> The bar is replaced by <c>DesktopDock</c>. This switches the
/// row off at <see cref="Awake"/>, which is what stops the eleven <see cref="UiWorldPreview"/> tiles under it
/// from ever waking up and building a camera each.</para>
///
/// <para>The row is switched off rather than the component destroyed because <c>planet_previews</c> carries
/// <b>two</b> of these - a live one and a legacy one with twenty slots - and removing either is prefab surgery
/// that wants a live editor (CS-088). Switching off the object they share retires both, and it costs one
/// <c>SetActive</c>. <c>GlobalMenuManager</c> reaches this through <c>FindObjectOfType</c>, which does not look
/// inside an inactive hierarchy, so its Reset button stops asking the bar to clear its selection by itself.</para>
/// </summary>
public class PlanetPreviewController : MonoBehaviour
{
    [SerializeField] private Button[] buttons;
    [SerializeField] private Vector3 lightDestinationPosition;
    [SerializeField] private Image selectionImage;

    [Tooltip("Diagnostic only: bring back the retired desktop planet bar. The dock replaces it.")]
    [SerializeField] private bool keepLegacyBar;

    private GameObject lightObject;
    private Vector3 lightInitialPosition;
    private bool movingLightToDestination;

    private void Awake()
    {
        if (!keepLegacyBar)
        {
            gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        if (selectionImage != null)
        {
            selectionImage.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // Subscribed in Start, so a row that was retired before Start never subscribed - unsubscribing twice is
        // harmless, leaving a destroyed object on a static event is not.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void OnButtonSelected(int index)
    {
        if (buttons != null && index >= 0 && index < buttons.Length)
        {
            OnButtonSelected(buttons[index]);
        }
    }

    public void OnButtonSelected(Button selectedButton)
    {
        // A coroutine cannot be started on an inactive object, and the light swing below is one. Nothing should
        // reach a retired row, but the caller is legacy menu code that finds its target at runtime.
        if (!isActiveAndEnabled)
        {
            return;
        }

        foreach (var button in buttons)
        {
            button.interactable = button != selectedButton;
        }

        if (lightObject == null)
        {
            lightObject = GameObject.Find("LightSourcePosition");
            if (lightObject != null)
            {
                lightInitialPosition = lightObject.transform.position;
            }
        }

        if (selectedButton != null)
        {
            if (!movingLightToDestination)
            {
                StartCoroutine(MoveLight(true));
            }
            selectionImage.gameObject.SetActive(true);
            selectionImage.transform.SetParent(selectedButton.transform);
            selectionImage.transform.localPosition = Vector3.zero;
        }
        else
        {
            StartCoroutine(MoveLight(false));
            selectionImage.gameObject.SetActive(false);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lightObject = null;
        if (selectionImage != null)
        {
            selectionImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator MoveLight(bool toDestination)
    {
        if (lightObject != null)
        {
            if (toDestination)
            {
                lightObject.transform.position = lightDestinationPosition;
                movingLightToDestination = true;
            }
            else
            {
                lightObject.transform.position = lightInitialPosition;
                movingLightToDestination = false;
            }
        }
        yield return null;
    }
}
