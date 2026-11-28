using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("참조 (비워두면 씬에서 자동으로 찾습니다)")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RabbitOxygenSystem oxygenSystem;

    [Header("게이지")]
    [SerializeField] private Slider oxygenSlider;
    [SerializeField] private Slider timeSlider;

    [Header("텍스트")]
    [SerializeField] private TMP_Text timeText;
    [Tooltip("탈출구 안내 같은 짧은 메시지")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float messageDuration = 2.5f;

    [Header("결과 화면")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private TMP_Text loseReasonText;

    private float messageTimer;

    private void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (oxygenSystem == null) oxygenSystem = FindFirstObjectByType<RabbitOxygenSystem>();

        if (gameManager == null)
        {
            Debug.LogWarning("[UIManager] GameManager를 찾지 못했습니다.", this);
        }
        else
        {
            gameManager.onGameWon.AddListener(ShowWin);
            gameManager.onGameLost.AddListener(ShowLose);
            gameManager.onMessage.AddListener(ShowMessage);
        }

        if (oxygenSystem == null)
            Debug.LogWarning("[UIManager] RabbitOxygenSystem을 찾지 못했습니다.", this);
        else
            oxygenSystem.onOxygenChanged.AddListener(OnOxygenChanged);

        // 초기 상태
        if (oxygenSlider != null) oxygenSlider.value = 1f;
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (messageText != null) messageText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.onGameWon.RemoveListener(ShowWin);
            gameManager.onGameLost.RemoveListener(ShowLose);
            gameManager.onMessage.RemoveListener(ShowMessage);
        }
        if (oxygenSystem != null)
            oxygenSystem.onOxygenChanged.RemoveListener(OnOxygenChanged);
    }

    private void Update()
    {
        UpdateTime();
        UpdateMessage();
    }

    // ───────────── 갱신 ─────────────

    private void UpdateTime()
    {
        if (gameManager == null) return;

        if (timeSlider != null) timeSlider.value = gameManager.TimeNormalized;

        if (timeText != null)
        {
            int total = Mathf.CeilToInt(gameManager.RemainingTime);
            timeText.text = $"{total / 60:00}:{total % 60:00}";
        }
    }

    private void OnOxygenChanged(float normalized)
    {
        if (oxygenSlider != null) oxygenSlider.value = normalized;
    }

    private void UpdateMessage()
    {
        if (messageText == null || !messageText.gameObject.activeSelf) return;

        messageTimer -= Time.unscaledDeltaTime;
        if (messageTimer <= 0f) messageText.gameObject.SetActive(false);
    }

    // ───────────── 이벤트 ─────────────

    private void ShowMessage(string message)
    {
        if (messageText == null) return;
        messageText.text = message;
        messageText.gameObject.SetActive(true);
        messageTimer = messageDuration;
    }

    private void ShowWin()
    {
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (winPanel != null) winPanel.SetActive(true);
    }

    private void ShowLose(GameManager.LoseReason reason)
    {
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (losePanel != null) losePanel.SetActive(true);

        if (loseReasonText != null)
        {
            switch (reason)
            {
                case GameManager.LoseReason.Caught:
                    loseReasonText.text = "경비에게 붙잡혔습니다!";
                    break;
                case GameManager.LoseReason.OxygenDepleted:
                    loseReasonText.text = "토끼의 산소가 모두 소진되었습니다.";
                    break;
                case GameManager.LoseReason.TimeUp:
                    loseReasonText.text = "제한 시간이 종료되었습니다.";
                    break;
            }
        }
    }
}