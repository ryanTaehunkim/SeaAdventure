using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("카메라가 따라갈 대상(거북이)을 여기에 드래그하세요.")]
    public Transform target; // 인스펙터에서 거북이의 Transform을 할당합니다.

    [Header("Camera Position")]
    [Tooltip("카메라가 대상 뒤쪽으로 얼마나 떨어질지 설정합니다 (Z축).")]
    public float distance = 5.0f;
    [Tooltip("카메라가 대상 위쪽으로 얼마나 높이 있을지 설정합니다 (Y축).")]
    public float height = 8.0f;

    [Header("Mouse Rotation")]
    [Tooltip("마우스를 좌우로 움직일 때 카메라가 회전하는 민감도입니다.")]
    public float mouseSensitivity = 2.0f;
    [Tooltip("켜면 게임 시작 시 마우스 커서를 숨기고 화면 중앙에 고정합니다. (Esc: 해제 / 클릭: 다시 고정)")]
    public bool lockCursor = true;

    [Header("Follow Speed")]
    [Range(1.0f, 10.0f)]
    [Tooltip("카메라가 대상을 따라가는 속도를 조절합니다. 값이 클수록 더 빨리 따라붙습니다.")]
    public float smoothSpeed = 5.0f;

    [Header("Collision (벽 가림 방지)")]
    [Tooltip("켜면 카메라와 대상 사이에 벽이 있을 때 카메라를 벽 앞으로 당겨옵니다.")]
    public bool avoidWalls = true;
    [Tooltip("카메라를 가릴 수 있는 레이어입니다. 벽 레이어만 선택하면 가장 정확합니다. (기본: 전부)")]
    public LayerMask collisionMask = ~0;
    [Tooltip("대상 위치에서 이만큼 위를 기준으로 검사합니다. (거북이 몸통 높이 정도)")]
    public float pivotHeight = 1.0f;
    [Tooltip("카메라의 두께입니다. 벽과 이 정도 간격을 유지합니다. 너무 크면 좁은 길에서 불안정할 수 있습니다.")]
    public float collisionRadius = 0.25f;
    [Tooltip("카메라가 대상에게 최대로 가까워질 수 있는 거리입니다.")]
    public float minDistance = 0.5f;

    // 카메라가 대상 주위를 도는 각도 (마우스로 조절)
    // 거북이가 바라보는 방향과 무관하게 움직이므로 이동 방향과 서로 영향을 주지 않습니다.
    private float yaw;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    private void Start()
    {
        yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
        SetCursorLocked(lockCursor);
    }

    private void Update()
    {
        if (EscapePressed()) SetCursorLocked(false);
        else if (lockCursor && ClickPressed()) SetCursorLocked(true);

        // 커서가 고정된 상태에서만 마우스로 회전
        if (Cursor.lockState == CursorLockMode.Locked)
            yaw += ReadMouseX() * mouseSensitivity;
    }

    private void LateUpdate() // LateUpdate는 모든 Update 로직이 끝난 후 실행됩니다.
    {
        // 1. 대상이 없으면 아무것도 하지 않습니다.
        if (target == null) return;

        // 2. 마우스로 정해진 각도(yaw) 기준으로 대상의 뒤쪽(distance)과 위쪽(height) 위치를 계산합니다.
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 desiredPosition = target.position + rotation * new Vector3(0f, height, -distance);
        Vector3 pivot = target.position + Vector3.up * pivotHeight;

        // 3. 목표 위치 자체를 벽 앞으로 보정합니다.
        //    (목표가 벽 안쪽이 아니라 항상 '벽 앞'이라서 카메라가 벽 쪽으로 파고들었다 튕기는 떨림이 생기지 않습니다.)
        if (avoidWalls)
            desiredPosition = ClampToWall(pivot, desiredPosition);

        // 4. 현재 위치에서 목표 위치까지 부드럽게 이동합니다. (프레임 속도와 무관한 보간)
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        Vector3 newPosition = Vector3.Lerp(transform.position, desiredPosition, t);

        // 5. 이동 도중(마우스 회전 등)에 벽을 통과하지 않도록 한 번 더 보정합니다.
        if (avoidWalls)
            newPosition = ClampToWall(pivot, newPosition);

        // 6. 카메라의 실제 위치를 갱신하고 대상을 바라봅니다.
        transform.position = newPosition;
        transform.LookAt(target);
    }

    /// <summary>
    /// pivot → point 사이에 벽이 있으면, 같은 직선 위에서 벽 바로 앞 지점으로 되돌린다.
    /// 두께가 있는 SphereCast를 써서 벽과 일정 간격(collisionRadius)이 유지된다.
    /// </summary>
    private Vector3 ClampToWall(Vector3 pivot, Vector3 point)
    {
        Vector3 dir = point - pivot;
        float dist = dir.magnitude;
        if (dist < 0.01f) return point;
        dir /= dist;

        int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, dir, hitBuffer, dist,
                                               collisionMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.transform.IsChildOf(target)) continue;   // 거북이 자신(과 등 위 토끼)은 무시
            if (h.distance < nearest) nearest = h.distance;
        }

        if (nearest == float.MaxValue) return point;       // 가리는 벽 없음

        float safeDistance = Mathf.Min(dist, Mathf.Max(minDistance, nearest));
        return pivot + dir * safeDistance;
    }

    // ───────────── 입력 (새/기존 Input 시스템 모두 지원) ─────────────

    private float ReadMouseX()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue().x * 0.1f : 0f;
#else
        return Input.GetAxis("Mouse X");
#endif
    }

    private bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private bool ClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(target.position + Vector3.up * pivotHeight, transform.position);
    }
}