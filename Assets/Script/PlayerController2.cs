using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController2 : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("가속·감속 정도 (클수록 즉각 반응)")]
    [SerializeField] private float acceleration = 20f;
    [Tooltip("이동 방향으로 몸을 돌리는 속도 (도/초)")]
    [SerializeField] private float turnSpeed = 540f;
    [Tooltip("모델의 앞면이 로컬 -Z라면 180. 이 값만큼 보정해서 모델 앞면이 이동 방향을 향합니다.")]
    [SerializeField] private float modelYawOffset = 180f;
    [SerializeField] private float gravity = -20f;

    [Header("카메라")]
    [Tooltip("비워두면 Main Camera를 자동으로 사용합니다. 반드시 Hierarchy 최상위에 있어야 합니다.")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private bool invertY = false;
    [Tooltip("카메라가 바라보는 중심의 높이 (캐릭터 발 기준)")]
    [SerializeField] private float pivotHeight = 1.2f;
    [SerializeField] private float cameraDistance = 4.5f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private float startPitch = 15f;
    [Tooltip("카메라가 캐릭터를 따라오는 부드러움 (클수록 빠르게 따라옴)")]
    [SerializeField] private float followSharpness = 12f;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float minCameraDistance = 0.8f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private bool lockCursor = true;

    [Header("토끼 구출·탑승")]
    [Tooltip("씬의 Rabbit 오브젝트(최상위)를 끌어다 넣습니다.")]
    [SerializeField] private Transform rabbit;
    [Tooltip("토끼가 얹힐 위치. 비워두면 거북이 자식으로 자동 생성합니다.")]
    [SerializeField] private Transform rabbitSeat;
    [Tooltip("RabbitSeat를 자동 생성할 때의 로컬 위치 (거북이 등 위로 조절)")]
    [SerializeField] private Vector3 seatLocalPosition = new Vector3(0f, 0.6f, 0f);
    [Tooltip("토끼가 업혔을 때 거북이 기준 Y 회전. 토끼가 거북이와 반대를 보면 180, 옆을 보면 90 / -90")]
    [SerializeField] private float rabbitYawOffset = 180f;
    [SerializeField] private float interactDistance = 2.5f;
    [Tooltip("토끼를 업고 있을 때 이동속도 배율")]
    [Range(0.1f, 1f)]
    [SerializeField] private float rideSpeedMultiplier = 0.6f;
    [Tooltip("비워두면 Rabbit(또는 씬)에서 RabbitOxygenSystem을 자동으로 찾습니다.")]
    [SerializeField] private RabbitOxygenSystem rabbitOxygen;

    [Header("이벤트")]
    public UnityEvent onRabbitRescued;

    public bool HasRabbit { get; private set; }
    public float CurrentSpeed => new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;

    private CharacterController cc;
    private float yaw;
    private float pitch;
    private Vector3 smoothPivot;
    private float currentDistance;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // 카메라 준비
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null)
        {
            Debug.LogError("[PlayerController2] 카메라를 찾지 못했습니다. Main Camera 태그를 확인하세요.", this);
        }
        else if (cameraTransform.IsChildOf(transform))
        {
            Debug.LogWarning("[PlayerController2] 카메라가 플레이어의 자식이어서 분리합니다. Hierarchy 최상위로 옮기는 것을 권장합니다.", this);
            cameraTransform.SetParent(null, true);
        }

        // 모델 보정값을 빼서, 모델의 앞면이 향한 방향을 카메라의 앞 방향으로 사용
        yaw = transform.eulerAngles.y - modelYawOffset;
        pitch = startPitch;
        smoothPivot = transform.position + Vector3.up * pivotHeight;
        currentDistance = cameraDistance;

        SetCursorLocked(lockCursor);

        // 토끼 준비
        if (rabbit == null)
            Debug.LogWarning("[PlayerController2] Rabbit이 연결되지 않았습니다. Inspector에서 연결하세요.", this);

        if (rabbitSeat == null)
        {
            GameObject seat = new GameObject("RabbitSeat");
            rabbitSeat = seat.transform;
            rabbitSeat.SetParent(transform, false);
            rabbitSeat.localPosition = seatLocalPosition;
        }

        if (rabbitOxygen == null)
        {
            if (rabbit != null) rabbitOxygen = rabbit.GetComponentInChildren<RabbitOxygenSystem>();
            if (rabbitOxygen == null) rabbitOxygen = FindFirstObjectByType<RabbitOxygenSystem>();
        }
    }

    private void Update()
    {
        HandleCursor();
        HandleLook();
        HandleMove();
        HandleInteract();
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    // ───────────── 커서 ─────────────

    private void HandleCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        if (lockCursor && Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            SetCursorLocked(true);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ───────────── 시점 ─────────────

    private void HandleLook()
    {
        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        yaw += delta.x * mouseSensitivity;
        pitch += (invertY ? delta.y : -delta.y) * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateCamera()
    {
        if (cameraTransform == null) return;

        Vector3 desiredPivot = transform.position + Vector3.up * pivotHeight;
        smoothPivot = Vector3.Lerp(smoothPivot, desiredPivot,
            1f - Mathf.Exp(-followSharpness * Time.deltaTime));

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 back = rot * Vector3.back;

        float allowed = GetAllowedDistance(smoothPivot, back);
        if (allowed < currentDistance)
            currentDistance = allowed; // 벽에 닿으면 즉시 당김
        else
            currentDistance = Mathf.Lerp(currentDistance, allowed,
                1f - Mathf.Exp(-6f * Time.deltaTime)); // 풀릴 때는 부드럽게

        cameraTransform.SetPositionAndRotation(smoothPivot + back * currentDistance, rot);
    }

    private float GetAllowedDistance(Vector3 origin, Vector3 dir)
    {
        float result = cameraDistance;
        RaycastHit[] hits = Physics.SphereCastAll(origin, collisionRadius, dir, cameraDistance,
            cameraCollisionMask, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.distance <= 0f) continue;
            if (hit.transform.IsChildOf(transform)) continue; // 자신과 업고 있는 토끼 제외
            if (rabbit != null && hit.transform.IsChildOf(rabbit)) continue;
            if (hit.distance < result) result = hit.distance;
        }

        return Mathf.Max(result, minCameraDistance);
    }

    // ───────────── 이동 ─────────────

    private void HandleMove()
    {
        Vector2 input = ReadMoveInput();
        Vector3 wishDir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);

        float speed = moveSpeed * (HasRabbit ? rideSpeedMultiplier : 1f);
        Vector3 targetVelocity = wishDir * speed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity,
            acceleration * Time.deltaTime);

        // 이동 방향으로 몸 돌리기
        if (wishDir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(wishDir) * Quaternion.Euler(0f, modelYawOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target,
                turnSpeed * Time.deltaTime);
        }

        // 중력
        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 move = horizontalVelocity + Vector3.up * verticalVelocity;
        cc.Move(move * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        Vector2 v = Vector2.zero;
        if (kb.wKey.isPressed) v.y += 1f;
        if (kb.sKey.isPressed) v.y -= 1f;
        if (kb.dKey.isPressed) v.x += 1f;
        if (kb.aKey.isPressed) v.x -= 1f;
        return v.sqrMagnitude > 1f ? v.normalized : v;
    }

    // ───────────── 토끼 구출·탑승 ─────────────

    private void HandleInteract()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        if (HasRabbit || rabbit == null) return;

        Vector3 toRabbit = rabbit.position - transform.position;
        toRabbit.y = 0f;

        if (toRabbit.magnitude > interactDistance)
        {
            Debug.Log("[PlayerController2] 토끼와 너무 멉니다.", this);
            return;
        }

        RescueRabbit();
    }

    private void RescueRabbit()
    {
        // 업힌 토끼가 거북이 이동·카메라·경비 시야를 방해하지 않도록 물리 요소를 끕니다.
        foreach (Collider col in rabbit.GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (Rigidbody rb in rabbit.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;

        rabbit.SetParent(rabbitSeat, true);
        rabbit.localPosition = Vector3.zero;
        rabbit.localRotation = Quaternion.Euler(0f, rabbitYawOffset, 0f);

        HasRabbit = true;
        Debug.Log("[PlayerController2] 토끼를 구출했습니다!", this);

        if (rabbitOxygen != null) rabbitOxygen.StartDrain();
        else Debug.LogWarning("[PlayerController2] RabbitOxygenSystem이 없어 산소 감소를 시작하지 못했습니다.", this);

        onRabbitRescued?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}