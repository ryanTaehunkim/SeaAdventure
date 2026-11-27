using UnityEngine;
using UnityEngine.Events;

public class RabbitOxygenSystem : MonoBehaviour
{
    [Header("산소 설정")]
    [SerializeField] private float maxOxygen = 100f;
    [Tooltip("초당 감소량")]
    [SerializeField] private float drainPerSecond = 3f;
    [Tooltip("안전 산소 구역 안에서 초당 회복량 (감소는 멈춥니다)")]
    [SerializeField] private float safeZoneRecoverPerSecond = 15f;

    [Header("테스트")]
    [Tooltip("체크하면 시작 즉시 산소가 줄어듭니다. 구출 연동 후에는 끄세요.")]
    [SerializeField] private bool startDrainingOnStart = false;

    [Header("이벤트")]
    [Tooltip("산소가 변할 때 0~1 비율로 호출 (UIManager의 Slider 연결용)")]
    public UnityEvent<float> onOxygenChanged;
    [Tooltip("산소가 0이 되면 한 번 호출 (GameManager의 실패 처리 연결용)")]
    public UnityEvent onOxygenDepleted;

    public float CurrentOxygen { get; private set; }
    public float Normalized => maxOxygen <= 0f ? 0f : CurrentOxygen / maxOxygen;
    public bool IsDraining { get; private set; }
    public bool IsDepleted { get; private set; }

    private int safeZoneCount;

    private void Start()
    {
        CurrentOxygen = maxOxygen;
        onOxygenChanged?.Invoke(Normalized);

        if (startDrainingOnStart) StartDrain();
    }

    private void Update()
    {
        if (!IsDraining || IsDepleted) return;

        if (safeZoneCount > 0)
            Restore(safeZoneRecoverPerSecond * Time.deltaTime);
        else
            Consume(drainPerSecond * Time.deltaTime);
    }

    // ───────────── 외부에서 호출 ─────────────

    /// <summary>토끼 구출 시 호출하면 산소 감소가 시작됩니다.</summary>
    public void StartDrain()
    {
        if (IsDepleted) return;
        IsDraining = true;
    }

    public void StopDrain() => IsDraining = false;

    /// <summary>공기 방울 등으로 산소를 회복합니다.</summary>
    public void Restore(float amount)
    {
        if (IsDepleted || amount <= 0f) return;
        SetOxygen(CurrentOxygen + amount);
    }

    public void EnterSafeZone() => safeZoneCount++;
    public void ExitSafeZone() => safeZoneCount = Mathf.Max(0, safeZoneCount - 1);

    /// <summary>재시작용 초기화</summary>
    public void ResetOxygen()
    {
        IsDepleted = false;
        IsDraining = false;
        safeZoneCount = 0;
        SetOxygen(maxOxygen);
    }

    // ───────────── 내부 ─────────────

    private void Consume(float amount)
    {
        SetOxygen(CurrentOxygen - amount);

        if (CurrentOxygen <= 0f && !IsDepleted)
        {
            IsDepleted = true;
            IsDraining = false;
            Debug.Log("[RabbitOxygen] 산소 소진!", this);
            onOxygenDepleted?.Invoke();
        }
    }

    private void SetOxygen(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxOxygen);
        if (Mathf.Approximately(clamped, CurrentOxygen)) return;

        CurrentOxygen = clamped;
        onOxygenChanged?.Invoke(Normalized);
    }
}