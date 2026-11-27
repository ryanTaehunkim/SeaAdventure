using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 속도 설정")]
    [Tooltip("토끼를 태우기 전 기본 이동 속도")]
    [SerializeField] private float normalSpeed = 6f;
    [Tooltip("토끼를 태운 후 이동 속도")]
    [SerializeField] private float carryingSpeed = 3.5f;
    [Tooltip("이동 방향을 향해 몸을 돌리는 속도")]
    [SerializeField] private float turnSpeed = 12f;
    [Tooltip("모델의 앞 방향 보정 (꼬리가 앞을 보면 180)")]
    [SerializeField, Range(-180f, 180f)] private float modelYawOffset = 180f;

    [Header("마우스 화면 회전 설정")]
    [Tooltip("마우스로 회전시킬 카메라 또는 카메라 피벗 오브젝트 (비워두면 Main Camera 사용)")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 65f;
    [Tooltip("게임 실행 중 마우스 커서를 화면 중앙에 고정할지 여부")]
    [SerializeField] private bool lockCursor = false;

    [Header("토끼(Rabbit) 탑승 설정")]
    [Tooltip("태울 토끼 오브젝트 (비워두면 이름으로 자동 검색)")]
    [SerializeField] private Transform rabbit;
    [SerializeField] private string rabbitObjectName = "Rabbit";
    [Tooltip("거북이 등 위 탑승 위치 (비워두면 자동 생성)")]
    [SerializeField] private Transform carryPoint;
    [Tooltip("거북이 기준 토끼 탑승 위치 오프셋 (실행 중에도 실시간 반영)")]
    [SerializeField] private Vector3 carryLocalPosition = new Vector3(0f, 0.25f, -0.1f);
    [Tooltip("토끼 바라보는 방향 미세 보정")]
    [SerializeField] private Vector3 carryLocalEulerAngles = Vector3.zero;
    [SerializeField] private float pickupRange = 3f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;

    private float yaw;
    private float pitch = 25f;
    private bool isCarryingRabbit;
    private bool autoCarryPointCreated;

    public bool IsCarryingRabbit => isCarryingRabbit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (cameraPivot == null && Camera.main != null)
            cameraPivot = Camera.main.transform;

        if (cameraPivot != null)
        {
            Vector3 angles = cameraPivot.eulerAngles;
            yaw = angles.y;
            pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Start()
    {
        if (rabbit == null && !string.IsNullOrEmpty(rabbitObjectName))
        {
            GameObject found = GameObject.Find(rabbitObjectName);
            if (found != null)
                rabbit = found.transform;
        }

        if (carryPoint == null)
        {
            GameObject pointObj = new GameObject("CarryPoint");
            carryPoint = pointObj.transform;
            carryPoint.SetParent(transform);
            autoCarryPointCreated = true;
        }

        ApplyCarryPointTransform();
    }

    private void Update()
    {
        ReadInputs();
        HandleCameraRotation();

        if (autoCarryPointCreated)
            ApplyCarryPointTransform();

        if (WasInteractPressed() && !isCarryingRabbit)
            TryPickUpRabbit();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void ReadInputs()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            moveInput = new Vector2(h, v);
        }
        else
        {
            moveInput = Vector2.zero;
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            lookInput = mouse.delta.ReadValue() * 0.1f;
        }
        else
        {
            lookInput = Vector2.zero;
        }
#else
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }

    private bool WasInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private void HandleCameraRotation()
    {
        if (cameraPivot == null) return;

        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        Vector3 clampedInput = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f);

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraPivot != null)
        {
            forward = cameraPivot.forward;
            forward.y = 0f;
            forward.Normalize();

            right = cameraPivot.right;
            right.y = 0f;
            right.Normalize();
        }

        Vector3 moveDir = forward * clampedInput.z + right * clampedInput.x;
        float currentSpeed = isCarryingRabbit ? carryingSpeed : normalSpeed;

        Vector3 horizontalVelocity = moveDir * currentSpeed;
        SetHorizontalVelocity(horizontalVelocity);

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
            Quaternion smoothRot = Quaternion.Slerp(rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(smoothRot);
        }
    }

    private void TryPickUpRabbit()
    {
        if (rabbit == null) return;

        float dist = Vector3.Distance(transform.position, rabbit.position);
        if (dist > pickupRange) return;

        foreach (Collider col in rabbit.GetComponentsInChildren<Collider>())
            col.enabled = false;

        Rigidbody rabbitRb = rabbit.GetComponent<Rigidbody>();
        if (rabbitRb != null)
            rabbitRb.isKinematic = true;

        rabbit.SetParent(carryPoint);
        rabbit.localPosition = Vector3.zero;
        rabbit.localRotation = Quaternion.identity;

        isCarryingRabbit = true;
    }

    private void ApplyCarryPointTransform()
    {
        if (carryPoint == null) return;

        carryPoint.localPosition = carryLocalPosition;
        // 거북이 모델이 180도 뒤집혀 있을 때도 토끼가 거북이와 같은 앞쪽을 보도록 보정
        carryPoint.localRotation = Quaternion.Euler(0f, -modelYawOffset, 0f) * Quaternion.Euler(carryLocalEulerAngles);
    }

    private Vector3 GetHorizontalVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        Vector3 v = rb.linearVelocity;
#else
        Vector3 v = rb.velocity;
#endif
        return new Vector3(v.x, 0f, v.z);
    }

    private void SetHorizontalVelocity(Vector3 horizontal)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
#else
        rb.velocity = new Vector3(horizontal.x, rb.velocity.y, horizontal.z);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        Vector3 previewPos = carryPoint != null
            ? carryPoint.position
            : transform.TransformPoint(carryLocalPosition);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(previewPos, 0.15f);
    }
}