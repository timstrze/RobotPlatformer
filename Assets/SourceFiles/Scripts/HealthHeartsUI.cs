using TMPro;
using UnityEngine;

/// <summary>
/// Shows three heart glyphs; filled color while the player has that much health, dimmed when lost.
/// Hearts are created at runtime if no <see cref="heartTexts"/> are assigned.
/// </summary>
public class HealthHeartsUI : MonoBehaviour
{
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] TextMeshProUGUI[] heartTexts;
    [SerializeField] Color fullColor = new Color(1f, 0.32f, 0.38f, 1f);
    [SerializeField] Color emptyColor = new Color(0.42f, 0.42f, 0.48f, 0.75f);
    [SerializeField] float heartSpacing = 48f;
    [SerializeField] float heartFontSize = 46f;

    const string HeartChar = "\u2665";

    void Awake()
    {
        EnsureHeartsBuilt();

        if (playerHealth == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerHealth = p.GetComponent<PlayerHealth>();
        }
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    void EnsureHeartsBuilt()
    {
        if (heartTexts != null && heartTexts.Length == 3 && heartTexts[0] != null)
            return;

        if (GetComponent<RectTransform>() == null)
            gameObject.AddComponent<RectTransform>();

        heartTexts = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"Heart_{i + 1}");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(8f + i * heartSpacing, -8f);
            r.sizeDelta = new Vector2(44f, 44f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = HeartChar;
            tmp.fontSize = heartFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = fullColor;
            tmp.raycastTarget = false;
            TmpDefaultFont.AssignIfEmpty(tmp);

            heartTexts[i] = tmp;
        }
    }

    void HandleHealthChanged(int current, int max)
    {
        if (heartTexts == null)
            return;
        int hp = Mathf.Min(current, max);
        int filledCount = Mathf.Min(hp, heartTexts.Length);
        for (int i = 0; i < heartTexts.Length; i++)
            heartTexts[i].color = i < filledCount ? fullColor : emptyColor;
    }
}
