using System.Collections;
using System.Collections.Generic;
using GalaxyExplorer;
using TMPro;
using UnityEngine;
using GalaxyExplorer.XR;

[RequireComponent(typeof(BoxCollider))]
public class POIBehavior : MonoBehaviour
{
    private const float INITIAL_DELAY = 5f; 
    
    [SerializeField] private List<Renderer> objectsToFade;
    [SerializeField] private TextMeshPro mainText;
    [SerializeField] private TextMeshPro subText;
    [SerializeField] private float alphaColor = .2f;
    [SerializeField] private float scale = 1f;
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private GEPressVisual pressableButton;
    
    
    private BoxCollider boxCollider;
    private GameObject cameraObject;
    private Transform[] corners;
    private bool fading;
    private List<Material> materialsToFade;
    private Vector3 colliderSize;
    private Color currentColor;
    private PointOfInterest poi;
    

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        poi = GetComponentInParent<CardPOI>();
        if (poi == null)
        {
            poi = GetComponentInParent<PointOfInterest>();
        }
        colliderSize = boxCollider.size;
        
        cameraObject = Camera.main.gameObject;
        var numberOfPoints = 7;
        corners = new Transform[numberOfPoints];
        Vector3[] verts = new Vector3[numberOfPoints]; 
        verts[0] = transform.TransformPoint(boxCollider.center);
        verts[1] = transform.TransformPoint(boxCollider.center + (new Vector3(boxCollider.size.x, boxCollider.size.y, 0) * 0.49f));
        verts[2] = transform.TransformPoint(boxCollider.center + (new Vector3(boxCollider.size.x, -boxCollider.size.y, 0) * 0.49f));
        verts[3] = transform.TransformPoint(boxCollider.center + (new Vector3(-boxCollider.size.x, boxCollider.size.y, 0) * 0.49f));
        verts[4] = transform.TransformPoint(boxCollider.center + (new Vector3(-boxCollider.size.x, -boxCollider.size.y, 0) * 0.49f));
        verts[5] = transform.TransformPoint(boxCollider.center + (new Vector3(0,-boxCollider.size.y, 0) * 0.49f));
        verts[6] = transform.TransformPoint(boxCollider.center + (new Vector3(0, boxCollider.size.y, 0) * 0.49f));

        for (var i = 0; i < verts.Length; i++)
        {
            var go = new GameObject($"Raycast Point {i}");
            go.transform.position = verts[i];
            go.transform.SetParent(transform, true);
            corners[i] = go.transform;
        }
        
        var anchor = new GameObject("Anchor");
        anchor.transform.SetParent(transform.parent, false);
        anchor.transform.localPosition = transform.localPosition;
        
        materialsToFade = new List<Material>();

        foreach (var fadingObject in objectsToFade)
        {
            materialsToFade.Add(fadingObject.material);
        }
        mainText.color = Color.clear;
        subText.color = Color.clear;
        Fade(false, 0, 0);
        Fade(false, INITIAL_DELAY, 0);
    }

    void Update()
    {
        if (fading)
        {
            return;
        }

        boxCollider.size = colliderSize;
        var allPointsVisible = true;
        for (int i = 0; i < corners.Length; i++)
        {
            if (!IsPointVisible(corners[i].position))
            {
                allPointsVisible = false;
                break;
            }
        }
        float alpha = GalaxyExplorerManager.Instance.CardPoiManager.IsAnyCardActive() ? 0f : -1f;
        Fade(allPointsVisible, alpha:alpha);
        transform.localPosition = offset;
        transform.localScale = scale * Vector3.one;
    }

    // Visible when nothing on the POI layer stands between the camera and this point (CS-297). This used to be a
    // one-slot RaycastNonAlloc of infinite length, and that slot is not guaranteed to hold the nearest hit: a label
    // on the far side of the galaxy could fill it, the label in front decided it was hidden, faded, and shrank its
    // own collider to nothing, so a click on its centre met nothing at all.
    private bool IsPointVisible(Vector3 position)
    {
        var layerMask = 1 << LayerMask.NameToLayer("POI");
        var origin = cameraObject.transform.position;
        var toPoint = position - origin;
        var distance = toPoint.magnitude;
        if (distance < 1e-4f)
        {
            return true;
        }

        if (!Physics.Raycast(origin, toPoint / distance, out var hit, distance + 0.01f, layerMask, QueryTriggerInteraction.Collide))
        {
            return true;
        }

        return hit.collider.gameObject == gameObject;
    }

    public void Fade(bool fadeIn, float overTime = .3f, float alpha = -1)
    {
        StartCoroutine(FadeRoutine(fadeIn, overTime, alpha, !poi.IsCardActive || poi is PlanetPOI));
        pressableButton.gameObject.SetActive(fadeIn);

    }

    private IEnumerator FadeRoutine(bool fadeIn, float overTime = .3f, float alpha = -1f, bool fadeMainText = true)
    {
        fading = true;
        alpha = alpha != -1 ? alpha : alphaColor;
        var fadingColor = fadeIn ? Color.white:  new Color(1,1,1,alpha);
        var startColor = currentColor;
      
        boxCollider.size = fadeIn ? colliderSize : Vector3.zero;
        var timeSoFar = 0f;
        while (timeSoFar < overTime)
        {
            foreach (var material in materialsToFade)
            {
                material.color = Color.Lerp(startColor, fadingColor, timeSoFar / overTime);
            }
        
            if (fadeMainText)
            {
                mainText.color = Color.Lerp(startColor, fadingColor, timeSoFar / overTime);
            }
            subText.color = Color.Lerp(startColor, fadingColor, timeSoFar / overTime);
            yield return null;
            timeSoFar += Time.deltaTime;
        }
        
        foreach (var material in materialsToFade)
        {
            material.color =  fadingColor;
        }
        
        if (fadeMainText)
        {
            mainText.color = fadingColor;
        }
        subText.color = fadingColor;

        currentColor = fadingColor;

        fading = false;
    }
}
