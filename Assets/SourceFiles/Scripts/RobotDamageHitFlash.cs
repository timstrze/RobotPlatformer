using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Briefly tints robot renderers (URP Lit <c>_BaseColor</c>) when damaged — e.g. from <see cref="GroundPatrolHazard"/>.
/// </summary>
public class RobotDamageHitFlash : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    [SerializeField] Color flashTint = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] float flashDuration = 0.45f;

    readonly List<ColorSlot> _slots = new();
    MaterialPropertyBlock _block;
    Coroutine _flashRoutine;

    struct ColorSlot
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Color BaseColor;
        public int ColorPropertyId;
    }

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (var i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null)
                    continue;
                var propId = BaseColorId;
                if (!m.HasProperty(BaseColorId))
                {
                    if (m.HasProperty(LegacyColorId))
                        propId = LegacyColorId;
                    else
                        continue;
                }
                _slots.Add(new ColorSlot
                {
                    Renderer = r,
                    MaterialIndex = i,
                    BaseColor = m.GetColor(propId),
                    ColorPropertyId = propId,
                });
            }
        }
    }

    public void PlayHitFlash()
    {
        if (_slots.Count == 0)
            return;
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        var elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            var u = Mathf.Clamp01(elapsed / flashDuration);
            var strength = Mathf.Sin(u * Mathf.PI);
            ApplyTint(strength);
            yield return null;
        }

        ApplyTint(0f);
        _flashRoutine = null;
    }

    void ApplyTint(float strength)
    {
        if (_block == null)
            return;
        foreach (var slot in _slots)
        {
            var c = Color.Lerp(slot.BaseColor, flashTint, strength);
            slot.Renderer.GetPropertyBlock(_block, slot.MaterialIndex);
            _block.SetColor(slot.ColorPropertyId, c);
            slot.Renderer.SetPropertyBlock(_block, slot.MaterialIndex);
        }
    }

    void OnDisable()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        ApplyTint(0f);
    }
}
