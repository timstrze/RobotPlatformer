using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// When the player loses all hearts, shows a full-screen modal and pauses the game.
/// Press Enter to reload the current scene (restores stars and hearts).
/// </summary>
public class GameOverController : MonoBehaviour
{
    [SerializeField] PlayerHealth playerHealth;
    [TextArea(3, 8)]
    [SerializeField] string gameOverMessage =
        "<align=center><size=120%><b>Game over</b></size></align>\n\n" +
        "Do you want to continue?\n\n" +
        "<color=#FFE8A0>Press Enter</color> to retry this level.\n" +
        "<size=85%>Your stars and hearts will be reset.</size>";

    GameObject _modalRoot;
    bool _gameOverShown;
    bool _reloadStarted;
#if ENABLE_INPUT_SYSTEM
    PlayerInput _disabledPlayerInput;
#endif

    void Awake()
    {
        BuildModal();
        if (_modalRoot != null)
            _modalRoot.SetActive(false);
    }

    void OnEnable()
    {
        ResolvePlayerHealth();
        if (playerHealth != null)
            playerHealth.OnDepleted += OnPlayerHealthDepleted;
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDepleted -= OnPlayerHealthDepleted;
    }

    void Update()
    {
        if (!_gameOverShown)
            return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return;
        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            ContinueAndReloadLevel();
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ContinueAndReloadLevel();
#endif
    }

    void ResolvePlayerHealth()
    {
        if (playerHealth != null)
            return;

        var found = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        if (found != null && found.Length > 0)
            playerHealth = found[0];
    }

    void BuildModal()
    {
        if (GetComponent<RectTransform>() == null)
            gameObject.AddComponent<RectTransform>();

        _modalRoot = new GameObject("GameOverModal");
        _modalRoot.layer = gameObject.layer;
        _modalRoot.transform.SetParent(transform, false);
        _modalRoot.transform.SetAsLastSibling();

        var rt = _modalRoot.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var dim = _modalRoot.AddComponent<Image>();
        dim.color = new Color(0.06f, 0.03f, 0.1f, 0.88f);
        dim.raycastTarget = true;

        var textGo = new GameObject("GameOverText");
        textGo.layer = gameObject.layer;
        textGo.transform.SetParent(_modalRoot.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(960f, 420f);
        trt.anchoredPosition = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = TmpDefaultFont.LiberationSansSafe(gameOverMessage);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 36f;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        TmpDefaultFont.AssignIfEmpty(tmp);
    }

    void OnPlayerHealthDepleted()
    {
        if (_gameOverShown)
            return;

        _gameOverShown = true;
        if (_modalRoot != null)
        {
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

#if ENABLE_INPUT_SYSTEM
        var inputs = FindObjectsByType<PlayerInput>(FindObjectsInactive.Exclude);
        if (inputs != null && inputs.Length > 0)
        {
            _disabledPlayerInput = inputs[0];
            _disabledPlayerInput.enabled = false;
        }
#endif
    }

    void ContinueAndReloadLevel()
    {
        if (!_gameOverShown || _reloadStarted)
            return;

        _reloadStarted = true;
        Time.timeScale = 1f;

#if ENABLE_INPUT_SYSTEM
        if (_disabledPlayerInput != null)
        {
            _disabledPlayerInput.enabled = true;
            _disabledPlayerInput = null;
        }
#endif

        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
    }
}
