using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Won, Lost }
    public enum LoseReason { Caught, OxygenDepleted, TimeUp }

    [Header("제한 시간")]
    [Tooltip("게임 시작부터 흐르는 제한 시간(초). 5분 = 300")]
    [SerializeField] private float timeLimit = 300f;

    [Header("자동 연결 (비워두면 씬에서 찾습니다)")]
    [SerializeField] private PlayerController2 player;
    [SerializeField] private RabbitOxygenSystem oxygenSystem;

    [Header("재시작")]
    [SerializeField] private bool allowRestartKey = true;

    [Header("이벤트")]
    public UnityEvent onGameWon;
    public UnityEvent<LoseReason> onGameLost;
    [Tooltip("안내 메시지 (UIManager가 구독합니다)")]
    public UnityEvent<string> onMessage;

    public GameState State { get; private set; } = GameState.Playing;
    public float RemainingTime { get; private set; }
    public float TimeNormalized => timeLimit <= 0f ? 0f : Mathf.Clamp01(RemainingTime / timeLimit);

    private GuardAI[] guards;

    private void Awake()
    {
        Time.timeScale = 1f;
        RemainingTime = timeLimit;
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController2>();
        if (oxygenSystem == null) oxygenSystem = FindFirstObjectByType<RabbitOxygenSystem>();

        if (player == null) Debug.LogWarning("[GameManager] PlayerController2를 찾지 못했습니다.", this);
        if (oxygenSystem == null) Debug.LogWarning("[GameManager] RabbitOxygenSystem을 찾지 못했습니다.", this);
        else oxygenSystem.onOxygenDepleted.AddListener(OnOxygenDepleted);

        guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        foreach (GuardAI guard in guards)
            guard.onPlayerCaught.AddListener(OnPlayerCaught);
    }

    private void OnDestroy()
    {
        if (oxygenSystem != null) oxygenSystem.onOxygenDepleted.RemoveListener(OnOxygenDepleted);
        if (guards != null)
        {
            foreach (GuardAI guard in guards)
                if (guard != null) guard.onPlayerCaught.RemoveListener(OnPlayerCaught);
        }
    }

    private void Update()
    {
        if (State == GameState.Playing)
        {
            RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
            if (RemainingTime <= 0f) LoseGame(LoseReason.TimeUp);
        }
        else if (allowRestartKey && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartGame();
        }
    }

    // ───────────── 외부에서 호출 ─────────────

    /// <summary>ExitGoal이 호출합니다.</summary>
    public void WinGame()
    {
        if (State != GameState.Playing) return;
        State = GameState.Won;
        Debug.Log("[GameManager] 승리!", this);
        FreezeGame();
        onGameWon?.Invoke();
    }

    public void LoseGame(LoseReason reason)
    {
        if (State != GameState.Playing) return;
        State = GameState.Lost;
        Debug.Log($"[GameManager] 패배: {reason}", this);
        FreezeGame();
        onGameLost?.Invoke(reason);
    }

    public void ShowMessage(string message)
    {
        if (State != GameState.Playing) return;
        onMessage?.Invoke(message);
    }

    /// <summary>재시작 버튼의 OnClick에 연결합니다.</summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ───────────── 내부 ─────────────

    private void OnPlayerCaught() => LoseGame(LoseReason.Caught);
    private void OnOxygenDepleted() => LoseGame(LoseReason.OxygenDepleted);

    private void FreezeGame()
    {
        if (oxygenSystem != null) oxygenSystem.StopDrain();
        if (player != null) player.enabled = false; // 이동·카메라 정지

        Time.timeScale = 0f; // 경비·애니메이션 등 모두 정지

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}