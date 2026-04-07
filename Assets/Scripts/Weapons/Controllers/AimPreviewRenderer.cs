using UnityEngine;
[DisallowMultipleComponent]
public class AimPreviewRenderer : MonoBehaviour
{
    private const float MinPreviewDirectionSqrMagnitude = 0.0001f;
    private const float DefaultLineAlpha = 0.2f;
    private const float DefaultShotFlashAlpha = 0.85f;
    private const float DefaultFlashDuration = 0.08f;
    private const float DefaultLineWidth = 0.045f;
    private const float DefaultHitPointScale = 0.12f;
    private const int DefaultPreviewMask = (1 << 0) | (1 << 6) | (1 << 7) | (1 << 9);

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private HitScanShooter shooter;

    [Header("Preview")]
    [SerializeField] private bool showAimPreview = true;
    [SerializeField] private LayerMask previewMask = DefaultPreviewMask;
    [SerializeField] private float previewDistance = 30f;
    [SerializeField] private float aimPreviewWidth = DefaultLineWidth;
    [SerializeField] [Range(0f, 1f)] private float aimPreviewAlpha = DefaultLineAlpha;
    [SerializeField] [Range(0f, 1f)] private float shotFlashAlpha = DefaultShotFlashAlpha;
    [SerializeField] private float shotFlashDuration = DefaultFlashDuration;
    [SerializeField] private float hitPointScale = DefaultHitPointScale;

    private LineRenderer _aimLineRenderer;
    private Transform _hitPointMarker;
    private Renderer _hitPointRenderer;
    private float _previewFlashUntilTime;

    private void Reset()
    {
        InitializeReferences();
    }

    private void Awake()
    {
        InitializeReferences();
        EnsurePreviewObjects();
        HidePreview();
    }

    public void SetFirePoint(Transform newFirePoint)
    {
        firePoint = newFirePoint != null ? newFirePoint : transform;
    }

    public void SetShooter(HitScanShooter newShooter)
    {
        shooter = newShooter;
    }

    public void SetPreviewDistance(float newDistance)
    {
        previewDistance = Mathf.Max(0.1f, newDistance);
    }

    public void UpdatePreview(Vector3 aimPoint, bool hasAimPoint)
    {
        if (!showAimPreview || firePoint == null || shooter == null || !hasAimPoint)
        {
            HidePreview();
            return;
        }

        if (!shooter.TryGetPreviewShot(out Vector3 origin, GetPreviewInputDirection(firePoint.position, aimPoint), out Vector3 previewDirection, out RaycastHit hit))
        {
            HidePreview();
            return;
        }
        
        bool hasHit = hit.collider != null;
        UpdatePreviewVisual(origin, previewDirection, hasHit, hit);
    }

    public void HidePreview()
    {
        if (_aimLineRenderer != null)
            _aimLineRenderer.enabled = false;

        if (_hitPointMarker != null)
            _hitPointMarker.gameObject.SetActive(false);
    }

    public void Flash()
    {
        _previewFlashUntilTime = Time.time + Mathf.Max(0f, shotFlashDuration);
    }

    private static Vector3 GetPreviewInputDirection(Vector3 origin, Vector3 aimPoint)
    {
        Vector3 previewDirection = aimPoint - origin;
        previewDirection.y = 0f;
        return previewDirection;
    }

    private void UpdatePreviewVisual(Vector3 origin, Vector3 previewDirection, bool hasHit, RaycastHit hit)
    {
        EnsurePreviewObjects();

        Vector3 endPoint = origin + previewDirection * previewDistance;
        if (hasHit)
            endPoint = origin + previewDirection * hit.distance;

        float currentAlpha = (Time.time <= _previewFlashUntilTime) ? shotFlashAlpha : aimPreviewAlpha;
        Color lineColor = new Color(1f, 0f, 0f, currentAlpha);

        _aimLineRenderer.enabled = true;
        _aimLineRenderer.startColor = lineColor;
        _aimLineRenderer.endColor = lineColor;
        _aimLineRenderer.startWidth = aimPreviewWidth;
        _aimLineRenderer.endWidth = aimPreviewWidth;
        _aimLineRenderer.SetPosition(0, origin);
        _aimLineRenderer.SetPosition(1, endPoint);

        if (_hitPointMarker == null)
            return;

        if (!hasHit)
        {
            _hitPointMarker.gameObject.SetActive(false);
            return;
        }

        _hitPointMarker.gameObject.SetActive(true);
        _hitPointMarker.position = endPoint;
        _hitPointMarker.localScale = Vector3.one * hitPointScale;

        if (_hitPointRenderer != null)
            ApplyMaterialColor(_hitPointRenderer.material, Color.red);
    }

    private void EnsurePreviewObjects()
    {
        if (_aimLineRenderer == null)
        {
            _aimLineRenderer = new GameObject("AimPreviewLine").AddComponent<LineRenderer>();
            _aimLineRenderer.transform.SetParent(transform, false);
            _aimLineRenderer.useWorldSpace = true;
            _aimLineRenderer.positionCount = 2;
            _aimLineRenderer.alignment = LineAlignment.View;
            _aimLineRenderer.textureMode = LineTextureMode.Stretch;
            _aimLineRenderer.numCapVertices = 6;
            _aimLineRenderer.startWidth = aimPreviewWidth;
            _aimLineRenderer.endWidth = aimPreviewWidth;
            _aimLineRenderer.material = CreateMaterial(Color.white);
            _aimLineRenderer.enabled = false;
        }

        if (_hitPointMarker == null)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = "AimPreviewHitPoint";
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localScale = Vector3.one * hitPointScale;

            Collider markerCollider = markerObject.GetComponent<Collider>();
            if (markerCollider != null)
            {
                markerCollider.enabled = false;
                Destroy(markerCollider);
            }

            _hitPointMarker = markerObject.transform;
            _hitPointRenderer = markerObject.GetComponent<Renderer>();
            if (_hitPointRenderer != null)
                _hitPointRenderer.material = CreateMaterial(Color.red);

            markerObject.SetActive(false);
        }
    }

    private void InitializeReferences()
    {
        firePoint ??= transform;
        shooter ??= GetComponentInChildren<HitScanShooter>();
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader);
        ApplyMaterialColor(material, color);
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
            return;
        }

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }
}
