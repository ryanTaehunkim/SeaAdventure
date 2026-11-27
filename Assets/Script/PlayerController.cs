using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Turtle(거북이) 조작 스크립트 (평면 이동용, 단독 동작)
///  - WASD : 카메라가 바라보는 방향(수평) 기준으로 이동
///  - E    : 가까이 있는 Rabbit(토끼)를 구출해서 등에 태움
///
/// 카메라는 별도 스크립트가 담당하며, 이 스크립트는 카메라 방향을 읽기만 한다.
/// 새 Input System / 기존 Input Manager 둘 다 지원한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 (Turtle)")]
    [SerializeField] private float speed = 5f;
    [SerializeField, Range(0.1f, 1f)] private float carrySpeedMultiplier = 0.55f; // 토끼를 업었을 때 속도 배율
    [SerializeField] private float turnSpeed = 12f;                              // 이동 방향으로 몸이 돌아가는 속도
    [SerializeField] private Transform cameraTransform;                          // 비워두면 Main Camera 사용

    [Header("토끼 (Rabbit)")]
    [SerializeField] private Transform rabbit;                                   // 비워두면 이름으로 자동 검색
    [SerializeField] private string rabbitObjectName = "Rabbit";
    [SerializeField] private Transform carryPoint;                               // 비워두면 등 위치에 자동 생성
    [SerializeField] private Vector3 carryLocalPosition = new Vector3(0f, 0.6f, -0.2f);
    [SerializeField] private float rescueRange = 2.5f;

    private Rigidbody rb;
    private Vector3 moveInput;

    public bool HasRabbit { get; private set; }
    public Transform Rabbit => rabbit;
    public float CurrentSpeedMultiplier => HasRabbit ? carrySpeedMultiplier : 1f;

    /// <summary>토끼가 구출 가능한 거리 안에 있는지 (UI에서 "E키로 구출" 표시 등에 활용)</summary>
    public bool IsRabbitInRange =>
        rabbit != null && !HasRabbit &&
        Vector3.Distance(transform.position, rabbit.position) <= rescueRange;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb.isKinematic)
            Debug.LogWarning("[PlayerController] Rigidbody의 Is Kinematic이 켜져 있어 해제합니다.", this);

        rb.isKinematic = false;                              // Kinematic이면 속도로 움직일 수 없음
        rb.useGravity = true;                                // 평면 위에서 이동하므로 중력 사용
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Inspector에서 위치 Freeze가 켜져 있어도 풀림
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        // 토끼 자동 검색 (Hierarchy에 "Rabbit" 이름의 오브젝트가 있으면 연결)
        if (rabbit == null)
        {
            GameObject found = GameObject.Find(rabbitObjectName);
            if (found != null) rabbit = found.transform;
            else Debug.LogWarning($"[PlayerController] '{rabbitObjectName}' 오브젝트를 찾지 못했습니다.", this);
        }

        // 등 위 탑승 위치 자동 생성
        if (carryPoint == null)
        {
            var go = new GameObject("RabbitCarryPoint");
            carryPoint = go.transform;
            carryPoint.SetParent(transform);
            carryPoint.localPosition = carryLocalPosition;
            carryPoint.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        Vector2 input = ReadMoveInput();

        // 카메라 기준 수평 방향 계산
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
        }
        forward.y = 0f;
        right.y = 0f;
        // 카메라가 바닥을 수직으로 내려다볼 때 방향이 0이 되는 경우 방지
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
        forward.Normalize();
        right.Normalize();

        moveInput = (forward * input.y + right * input.x).normalized;

        if (InteractPressed())
            TryRescueRabbit();
    }

    private void FixedUpdate()
    {
        Vector3 horizontal = moveInput * (speed * CurrentSpeedMultiplier);

        // 수평 속도만 덮어쓰고, 수직(중력) 속도는 그대로 유지
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
#else
        rb.velocity = new Vector3(horizontal.x, rb.velocity.y, horizontal.z);
#endif

        // 이동 방향으로 몸 회전
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(moveInput, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, turnSpeed * Time.fixedDeltaTime));
        }
    }

    // ───────────────── 입력 (새/기존 Input 시스템 모두 지원) ─────────────────

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        return new Vector2(h, v);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    private bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    // ───────────────── 토끼 구출 ─────────────────

    private void TryRescueRabbit()
    {
        if (HasRabbit || rabbit == null) return;

        if (!IsRabbitInRange)
        {
            Debug.Log("토끼에게 더 가까이 가야 한다!");
            return;
        }

        // 토끼의 충돌/물리를 끄고 등 위(carryPoint)에 자식으로 붙임
        foreach (var col in rabbit.GetComponentsInChildren<Collider>())
            col.enabled = false;

        var rabbitRb = rabbit.GetComponent<Rigidbody>();
        if (rabbitRb != null) rabbitRb.isKinematic = true;

        rabbit.SetParent(carryPoint);
        rabbit.localPosition = Vector3.zero;
        rabbit.localRotation = Quaternion.identity;

        HasRabbit = true;
        Debug.Log("Rabbit를 구출했다!");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rescueRange);
    }
}
