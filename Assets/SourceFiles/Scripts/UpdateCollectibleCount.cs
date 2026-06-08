using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[DefaultExecutionOrder(-1000)]
public class UpdateCollectibleCount : MonoBehaviour
{
    private static Sprite _sharedUiSprite;

    [Header("Completion")]
    [SerializeField] private string successMessage = "All stars collected! Amazing work, Adam and Izzy!";
    [SerializeField] private string nextSceneName = "";
    [SerializeField] private float delayBeforeLoadSeconds = 2f;
    [Tooltip("When a next scene is set, wait for the Start button instead of auto-loading after the delay.")]
    [SerializeField] private bool requireButtonClickForNextScene = true;
    [SerializeField] private GameObject nextLevelStartButton;

    [Header("Bonus wave (optional, e.g. Level 1)")]
    [Tooltip("When set, clearing the first wave spawns bonus stars instead of ending. Must match the active scene name.")]
    [SerializeField] private string bonusWaveSceneName = "";
    [SerializeField] private GameObject bonusPickupPrefab;
    [SerializeField] private int bonusStarCount = 5;
    [SerializeField] private Vector3 bonusRingCenter = new Vector3(0f, 2.2f, -4f);
    [SerializeField] private float bonusRingRadius = 7f;
    [TextArea(2, 5)]
    [SerializeField] private string bonusWaveCelebrationMessage =
        "<align=center><b>You got them all!</b></align>\n<size=140%><color=#FFE8A0>Amazing work, Adam and Izzy!</color></size>\n\nBonus stars just appeared—go grab them!";

    [Header("Fireworks (optional)")]
    [SerializeField] private Material fireworkParticleMaterial;
    [SerializeField] private int fireworksBurstCount = 7;
    [SerializeField] private float fireworksRingRadius = 11f;
    [SerializeField] private float fireworksHeightOffset = 2f;
    [SerializeField] private float fireworksBurstDelay = 0.09f;

    private TextMeshProUGUI collectibleText;
    private int collectiblesAtStart;
    private bool allCollected;
    private bool loadScheduled;
    private bool bonusWaveSpawned;
    private float bonusCelebrationHoldUntilUnscaled;
    private bool startButtonClickHooked;
    private RectTransform _backgroundPanel;
    private Vector2 _backgroundDefaultSize;
    private Vector2 _backgroundDefaultPos;
    private float _defaultFontSizeMax;
    private Image _backgroundImage;
    private Color _backgroundImageDefaultColor;
    private GameObject _celebrationChromeRoot;
    private bool _celebrationChromeIsFullWin;
    private Coroutine _celebrationMotionRoutine;
    private bool _celebrationVisualsActive;
    private float _defaultLineSpacing;
    private float _defaultCharacterSpacing;
    private float _defaultParagraphSpacing;
    private float _savedOutlineWidth;
    private Color _savedOutlineColor;
    private bool _hadGlowKeyword;
    private UnityEngine.UI.Shadow _textDropShadow;
    private Vector2 _textShadowDefaultDistance;

    void Awake()
    {
        SanitizeSerializedMessagesAndTmp();
    }

    void OnEnable()
    {
        SanitizeSerializedMessagesAndTmp();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        successMessage = TmpDefaultFont.LiberationSansSafe(successMessage);
        bonusWaveCelebrationMessage = TmpDefaultFont.LiberationSansSafe(bonusWaveCelebrationMessage);
        if (!Application.isPlaying)
        {
            var tmp = GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = TmpDefaultFont.LiberationSansSafe(tmp.text);
        }
    }
#endif

    void SanitizeSerializedMessagesAndTmp()
    {
        successMessage = TmpDefaultFont.LiberationSansSafe(successMessage);
        bonusWaveCelebrationMessage = TmpDefaultFont.LiberationSansSafe(bonusWaveCelebrationMessage);
        var tmp = GetComponent<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = TmpDefaultFont.LiberationSansSafe(tmp.text);
    }

    void Start()
    {
        collectibleText = GetComponent<TextMeshProUGUI>();
        if (collectibleText == null)
        {
            Debug.LogError("UpdateCollectibleCount script requires a TextMeshProUGUI component on the same GameObject.");
            return;
        }

        // HUD text must not steal pointer hits from the Start button (TMP mesh bounds can extend past the panel).
        collectibleText.raycastTarget = false;

        _backgroundPanel = collectibleText.transform.parent as RectTransform;
        if (_backgroundPanel != null)
        {
            _backgroundDefaultSize = _backgroundPanel.sizeDelta;
            _backgroundDefaultPos = _backgroundPanel.anchoredPosition;
            _backgroundImage = _backgroundPanel.GetComponent<Image>();
            if (_backgroundImage != null)
                _backgroundImageDefaultColor = _backgroundImage.color;
        }

        _defaultFontSizeMax = collectibleText.fontSizeMax;
        _defaultLineSpacing = collectibleText.lineSpacing;
        _defaultCharacterSpacing = collectibleText.characterSpacing;
        _defaultParagraphSpacing = collectibleText.paragraphSpacing;

        _textDropShadow = GetComponent<UnityEngine.UI.Shadow>();
        if (_textDropShadow != null)
            _textShadowDefaultDistance = _textDropShadow.effectDistance;

        CacheMaterialDefaultsFromText();

        collectiblesAtStart = FindObjectsByType(
            typeof(Pickup),
            FindObjectsInactive.Exclude).Length;

        if (!string.IsNullOrWhiteSpace(nextSceneName) && requireButtonClickForNextScene)
            EnsureNextLevelStartButton();

        HookStartButtonClick();
        SetNextLevelButtonVisible(false);
        UpdateCollectibleDisplay();
    }

    void Update()
    {
        if (!allCollected)
            UpdateCollectibleDisplay();
        else
            TryConfirmNextLevelByKeyboard();
    }

    private void UpdateCollectibleDisplay()
    {
        if (bonusCelebrationHoldUntilUnscaled > 0f &&
            Time.unscaledTime < bonusCelebrationHoldUntilUnscaled &&
            !allCollected)
        {
            collectibleText.text = TmpDefaultFont.LiberationSansSafe(bonusWaveCelebrationMessage);
            return;
        }

        int remaining = FindObjectsByType(
            typeof(Pickup),
            FindObjectsInactive.Exclude).Length;

        if (remaining == 0 && collectiblesAtStart > 0)
        {
            if (ShouldSpawnBonusWave())
            {
                SpawnBonusWave();
                ExpandCelebrationPanel(applyFullWinPresentation: false);
                collectibleText.text = TmpDefaultFont.LiberationSansSafe(bonusWaveCelebrationMessage);
                bonusCelebrationHoldUntilUnscaled = Time.unscaledTime + 4f;
                StartCoroutine(PlayBonusWaveFireworks());
                return;
            }

            ExpandCelebrationPanel(applyFullWinPresentation: true);
            collectibleText.text = TmpDefaultFont.LiberationSansSafe(successMessage);
            allCollected = true;
            StartCoroutine(PlayFinaleFireworks());
            HandleNextSceneTransition();
            return;
        }

        collectibleText.text =
            $"<b>Stars to find</b>  <color=#FFF0B8>{remaining}</color>";
        ResetPanelToGameplay();
    }

    private void ResetPanelToGameplay()
    {
        if (allCollected || _backgroundPanel == null)
            return;

        ClearCelebrationPresentation();

        _backgroundPanel.sizeDelta = _backgroundDefaultSize;
        _backgroundPanel.anchoredPosition = _backgroundDefaultPos;
        collectibleText.fontSizeMax = _defaultFontSizeMax;
    }

    private bool ShouldSpawnBonusWave()
    {
        if (bonusWaveSpawned || bonusPickupPrefab == null ||
            string.IsNullOrWhiteSpace(bonusWaveSceneName))
            return false;

        return SceneManager.GetActiveScene().name == bonusWaveSceneName;
    }

    private void SpawnBonusWave()
    {
        bonusWaveSpawned = true;

        Transform parent = null;
        var collectiblesRoot = GameObject.Find("Collectibles");
        if (collectiblesRoot != null)
            parent = collectiblesRoot.transform;

        int n = Mathf.Max(1, bonusStarCount);
        for (int i = 0; i < n; i++)
        {
            float t = (i / (float)n) * Mathf.PI * 2f;
            Vector3 pos = bonusRingCenter +
                          new Vector3(Mathf.Cos(t) * bonusRingRadius, 0f, Mathf.Sin(t) * bonusRingRadius);
            // Non-generic Instantiate avoids InvalidCastException from Instantiate<GameObject> when the
            // Inspector slot references a non-GameObject Object (wrong asset type or broken reference).
            Object created = Object.Instantiate((Object)bonusPickupPrefab, pos, Quaternion.identity);
            GameObject instance = created as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "UpdateCollectibleCount: bonusPickupPrefab must be a GameObject prefab (e.g. the star with Pickup). " +
                    "Clear the field or assign the correct prefab on this component.",
                    this);
                continue;
            }

            if (parent != null)
                instance.transform.SetParent(parent, true);
        }
    }

    private IEnumerator PlayBonusWaveFireworks()
    {
        yield return StartCoroutine(CelebrationFireworks.PlayRingBurstsCoroutine(
            bonusRingCenter,
            fireworksBurstCount,
            fireworksRingRadius,
            fireworksHeightOffset,
            fireworkParticleMaterial,
            fireworksBurstDelay));
    }

    private IEnumerator PlayFinaleFireworks()
    {
        yield return null;
        Vector3 c = bonusRingCenter + Vector3.up * (fireworksHeightOffset + 1.5f);
        for (int k = 0; k < 5; k++)
        {
            CelebrationFireworks.PlayBurst(c + Random.insideUnitSphere * 2f, fireworkParticleMaterial);
            yield return new WaitForSeconds(0.12f);
        }
    }

    private void TryScheduleNextScene()
    {
        if (loadScheduled || string.IsNullOrWhiteSpace(nextSceneName))
            return;

        loadScheduled = true;
        StartCoroutine(LoadNextSceneAfterDelay());
    }

    private void HandleNextSceneTransition()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
            return;

        if (requireButtonClickForNextScene)
        {
            EnsureNextLevelStartButton();
            HookStartButtonClick();
            Time.timeScale = 0f;
            SetNextLevelButtonVisible(true);
            return;
        }

        TryScheduleNextScene();
    }

    public void OnStartNextLevelButtonClicked()
    {
        if (!allCollected || loadScheduled || string.IsNullOrWhiteSpace(nextSceneName))
            return;

        loadScheduled = true;
        LoadNextSceneNow();
    }

    private void TryConfirmNextLevelByKeyboard()
    {
        if (loadScheduled || string.IsNullOrWhiteSpace(nextSceneName) || !requireButtonClickForNextScene)
            return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return;
        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            OnStartNextLevelButtonClicked();
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnStartNextLevelButtonClicked();
#endif
    }

    private void ExpandCelebrationPanel(bool applyFullWinPresentation)
    {
        if (_backgroundPanel == null)
            return;

        _backgroundPanel.sizeDelta = new Vector2(980f, 340f);
        _backgroundPanel.anchoredPosition = new Vector2(0f, -62f);
        collectibleText.fontSizeMax = Mathf.Max(_defaultFontSizeMax, 88f);

        ApplyCelebrationPresentation(applyFullWinPresentation);
    }

    private void CacheMaterialDefaultsFromText()
    {
        Material m = collectibleText.fontMaterial;
        if (m.HasProperty(ShaderUtilities.ID_OutlineWidth))
            _savedOutlineWidth = m.GetFloat(ShaderUtilities.ID_OutlineWidth);
        if (m.HasProperty(ShaderUtilities.ID_OutlineColor))
            _savedOutlineColor = m.GetColor(ShaderUtilities.ID_OutlineColor);
        _hadGlowKeyword = m.IsKeywordEnabled(ShaderUtilities.Keyword_Glow);
    }

    private void ApplyCelebrationPresentation(bool fullWin)
    {
        _celebrationVisualsActive = true;

        if (_backgroundImage != null)
        {
            // Deep cosmic panel + slight magenta lift at the edges (read as "arcade victory")
            _backgroundImage.color = new Color(0.09f, 0.04f, 0.22f, 0.97f);
        }

        collectibleText.lineSpacing = fullWin ? 32f : 26f;
        collectibleText.paragraphSpacing = fullWin ? 12f : 8f;
        collectibleText.characterSpacing = fullWin ? 1.8f : 1.2f;

        if (_textDropShadow != null)
            _textDropShadow.effectDistance = new Vector2(1.75f, -2.1f);

        ApplyCelebrationTextMaterial(fullWin);
        EnsureCelebrationChrome(fullWin);
        RestartCelebrationMotion();
    }

    private void ApplyCelebrationTextMaterial(bool fullWin)
    {
        Material m = collectibleText.fontMaterial;
        if (m.HasProperty(ShaderUtilities.ID_OutlineWidth))
        {
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, fullWin ? 0.38f : 0.28f);
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(1f, 0.25f, 0.72f, 1f));
        }

        if (fullWin && m.HasProperty(ShaderUtilities.ID_GlowColor))
        {
            m.EnableKeyword(ShaderUtilities.Keyword_Glow);
            m.SetColor(ShaderUtilities.ID_GlowColor, new Color(1f, 0.92f, 0.35f, 0.75f));
            if (m.HasProperty(ShaderUtilities.ID_GlowOffset))
                m.SetFloat(ShaderUtilities.ID_GlowOffset, 6f);
            if (m.HasProperty(ShaderUtilities.ID_GlowOuter))
                m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.42f);
        }
        else if (m.HasProperty(ShaderUtilities.ID_GlowColor) && !_hadGlowKeyword)
        {
            m.DisableKeyword(ShaderUtilities.Keyword_Glow);
        }

        collectibleText.ForceMeshUpdate();
    }

    private void ResetCelebrationTextMaterial()
    {
        Material m = collectibleText.fontMaterial;
        if (m.HasProperty(ShaderUtilities.ID_OutlineWidth))
        {
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, _savedOutlineWidth);
            m.SetColor(ShaderUtilities.ID_OutlineColor, _savedOutlineColor);
            if (_savedOutlineWidth <= 0.0001f)
                m.DisableKeyword(ShaderUtilities.Keyword_Outline);
        }

        if (m.HasProperty(ShaderUtilities.ID_GlowColor))
        {
            if (!_hadGlowKeyword)
                m.DisableKeyword(ShaderUtilities.Keyword_Glow);
        }

        collectibleText.ForceMeshUpdate();
    }

    private void EnsureCelebrationChrome(bool fullWin)
    {
        if (_backgroundPanel == null)
            return;

        if (_celebrationChromeRoot != null)
        {
            if (_celebrationChromeIsFullWin == fullWin)
            {
                _celebrationChromeRoot.SetActive(true);
                return;
            }

            Destroy(_celebrationChromeRoot);
            _celebrationChromeRoot = null;
        }

        _celebrationChromeIsFullWin = fullWin;

        _celebrationChromeRoot = new GameObject("CelebrationChrome");
        _celebrationChromeRoot.layer = _backgroundPanel.gameObject.layer;
        var rootRt = _celebrationChromeRoot.AddComponent<RectTransform>();
        rootRt.SetParent(_backgroundPanel, false);
        rootRt.SetAsFirstSibling();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Sprite sprite = GetSharedUiSprite();

        // Outer warm "neon" rim (drawn behind text, on top of panel image)
        var rimGo = new GameObject("OuterRim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rimGo.layer = _celebrationChromeRoot.layer;
        rimGo.transform.SetParent(_celebrationChromeRoot.transform, false);
        var rimRt = rimGo.GetComponent<RectTransform>();
        rimRt.anchorMin = Vector2.zero;
        rimRt.anchorMax = Vector2.one;
        rimRt.offsetMin = new Vector2(-10f, -10f);
        rimRt.offsetMax = new Vector2(10f, 10f);
        var rimImg = rimGo.GetComponent<Image>();
        rimImg.sprite = sprite;
        rimImg.color = fullWin ? new Color(1f, 0.45f, 0.85f, 0.55f) : new Color(1f, 0.65f, 0.35f, 0.45f);
        rimImg.raycastTarget = false;

        AddChromeStrip(_celebrationChromeRoot.transform, sprite, "TopGold", true,
            new Color(1f, 0.92f, 0.45f, 0.95f));
        AddChromeStrip(_celebrationChromeRoot.transform, sprite, "BottomCyan", false,
            new Color(0.45f, 0.95f, 1f, 0.85f));

        if (fullWin)
        {
            AddChromeStrip(_celebrationChromeRoot.transform, sprite, "MidRibbon", true,
                new Color(1f, 0.35f, 0.55f, 0.35f), yFromTop: -18f, height: 6f);
        }
    }

    private static void AddChromeStrip(Transform parent, Sprite sprite, string name, bool top, Color c,
        float yFromTop = 0f, float height = 7f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yFromTop);
            rt.sizeDelta = new Vector2(0f, height);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
        }

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = c;
        img.raycastTarget = false;
    }

    private static Sprite GetSharedUiSprite()
    {
        if (_sharedUiSprite != null)
            return _sharedUiSprite;

        var tex = Texture2D.whiteTexture;
        _sharedUiSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return _sharedUiSprite;
    }

    private void RestartCelebrationMotion()
    {
        if (_celebrationMotionRoutine != null)
            StopCoroutine(_celebrationMotionRoutine);
        _celebrationMotionRoutine = StartCoroutine(CelebrationMotionLoop());
    }

    private IEnumerator CelebrationMotionLoop()
    {
        if (_backgroundPanel == null)
            yield break;

        // Pop-in
        float intro = 0f;
        const float introDur = 0.38f;
        var baseScale = Vector3.one;
        while (intro < introDur)
        {
            intro += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(intro / introDur);
            float s = Mathf.SmoothStep(0.88f, 1f, t);
            _backgroundPanel.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        _backgroundPanel.localScale = baseScale;

        while (_celebrationVisualsActive)
        {
            float wobble = Mathf.Sin(Time.unscaledTime * 2.1f);
            float breathe = 1f + Mathf.Sin(Time.unscaledTime * 2.8f) * 0.012f;
            _backgroundPanel.localScale = new Vector3(breathe, breathe, 1f);
            _backgroundPanel.localRotation = Quaternion.Euler(0f, 0f, wobble * 1.4f);
            yield return null;
        }
    }

    private void ClearCelebrationPresentation()
    {
        _celebrationVisualsActive = false;

        if (_celebrationMotionRoutine != null)
        {
            StopCoroutine(_celebrationMotionRoutine);
            _celebrationMotionRoutine = null;
        }

        if (_backgroundPanel != null)
        {
            _backgroundPanel.localScale = Vector3.one;
            _backgroundPanel.localRotation = Quaternion.identity;
        }

        if (_celebrationChromeRoot != null)
        {
            Destroy(_celebrationChromeRoot);
            _celebrationChromeRoot = null;
            _celebrationChromeIsFullWin = false;
        }

        if (_backgroundImage != null)
            _backgroundImage.color = _backgroundImageDefaultColor;

        collectibleText.lineSpacing = _defaultLineSpacing;
        collectibleText.characterSpacing = _defaultCharacterSpacing;
        collectibleText.paragraphSpacing = _defaultParagraphSpacing;

        if (_textDropShadow != null)
            _textDropShadow.effectDistance = _textShadowDefaultDistance;

        ResetCelebrationTextMaterial();
    }

    private void EnsureNextLevelStartButton()
    {
        if (nextLevelStartButton != null)
            return;

        EnsureEventSystemExists();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning(
                "UpdateCollectibleCount: No Canvas found in parents; assign Next Level Start Button in the Inspector or add this UI under a Canvas.",
                this);
            return;
        }

        var go = new GameObject(
            "NextLevel_Start_Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 112f);
        rt.sizeDelta = new Vector2(360f, 76f);

        var img = go.GetComponent<Image>();
        var sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        img.sprite = sprite;
        img.color = new Color(0.22f, 0.52f, 0.95f, 1f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.layer = go.layer;
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "Start";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 36f;
        tmp.fontWeight = FontWeight.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        TmpDefaultFont.AssignIfEmpty(tmp);

        nextLevelStartButton = go;
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
            return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGo.AddComponent<InputSystemUIInputModule>();
#else
        esGo.AddComponent<StandaloneInputModule>();
#endif
    }

    private void HookStartButtonClick()
    {
        if (startButtonClickHooked || nextLevelStartButton == null)
            return;

        var button = nextLevelStartButton.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.AddListener(OnStartNextLevelButtonClicked);
        startButtonClickHooked = true;
    }

    private void SetNextLevelButtonVisible(bool isVisible)
    {
        if (nextLevelStartButton != null)
            nextLevelStartButton.SetActive(isVisible);
    }

    private IEnumerator LoadNextSceneAfterDelay()
    {
        if (delayBeforeLoadSeconds > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeLoadSeconds);

        LoadNextSceneNow();
    }

    private void LoadNextSceneNow()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneName);
    }
}
