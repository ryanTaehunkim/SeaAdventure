using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("카메라가 따라갈 대상(거북이)을 여기에 드래그하세요.")]
    public Transform target; // 인스펙터에서 거북이의 Transform을 할당합니다.
    [Tooltip("Target이 비어 있으면 이 이름의 오브젝트를 자동으로 찾아 따라갑니다.")]
    public string targetObjectName = "colobus";

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

    [Header("Vertical Look (마우스 상하 회전)")]
    [Tooltip("켜면 마우스를 위아래로 움직여 카메라 각도를 조절합니다. 끄면 height/distance 값으로 각도가 고정됩니다.")]
    public bool enableVerticalLook = true;
    [Tooltip("마우스 위/아래 방향을 반대로 합니다.")]
    public bool invertY = false;
    [Tooltip("카메라가 내려갈 수 있는 최소 각도입니다. (수평=0, 바로 위=90)")]
    [Range(0f, 89f)] public float minPitch = 10f;
    [Tooltip("카메라가 올라갈 수 있는 최대 각도입니다. 90에 가까우면 화면이 뒤집힐 수 있어 80 정도를 권장합니다.")]
    [Range(0f, 89f)] public float maxPitch = 80f;

    [Header("Follow Speed")]
    [Range(1.0f, 10.0f)]
    [Tooltip("카메라가 대상을 따라가는 속도를 조절합니다. 값이 클수록 더 빨리 따라붙습니다.")]
    public float smoothSpeed = 5.0f;

    // 카메라가 대상 주위를 도는 각도 (마우스로 조절)
    // 거북이가 바라보는 방향과 무관하게 움직이므로 이동 방향과 서로 영향을 주지 않습니다.
    private float yaw;
    private float pitch;   // 카메라가 대상을 내려다보는 각도 (마우스 상하 이동으로 조절)

    // height / distance 값에서 계산되는 기본 각도와 대상까지의 거리
    private float BasePitch => Mathf.Atan2(height, distance) * Mathf.Rad2Deg;
    private float Radius => Mathf.Sqrt(height * height + distance * distance);

    private void Start()
    {
        // Target이 비어 있으면 이름으로 자동 검색
        if (target == null)
        {
            GameObject found = GameObject.Find(targetObjectName);
            if (found != null) target = found.transform;
            else Debug.LogWarning($"[CameraController] Target이 비어 있고 '{targetObjectName}' 오브젝트도 찾지 못했습니다. Inspector에서 Target을 연결해 주세요.", this);
        }

        yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
        pitch = Mathf.Clamp(BasePitch, minPitch, maxPitch);   // 처음에는 height/distance 위치에서 시작
        SetCursorLocked(lockCursor);
    }

    private void Update()
    {
        if (EscapePressed()) SetCursorLocked(false);
        else if (lockCursor && ClickPressed()) SetCursorLocked(true);

        // 커서가 고정된 상태에서만 마우스로 회전
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += ReadMouseX() * mouseSensitivity;

            if (enableVerticalLook)
            {
                // 마우스를 위로 올리면 카메라가 낮아져 위를 보게 됨 (invertY로 반전 가능)
                float dir = invertY ? 1f : -1f;
                pitch += ReadMouseY() * mouseSensitivity * dir;
            }
        }

        if (enableVerticalLook) pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        else pitch = BasePitch;   // 상하 회전을 끄면 height/distance 값으로 고정 (실행 중 수정도 바로 반영)
    }

    private void LateUpdate() // LateUpdate는 모든 Update 로직이 끝난 후 실행됩니다.
    {
        // 1. 대상이 없으면 아무것도 하지 않습니다.
        if (target == null) return;

        // 2. 마우스로 정해진 각도(yaw, pitch) 기준으로 대상으로부터 Radius만큼 떨어진 위치를 계산합니다.
        //    벽 등 주변 물체와는 상관없이 항상 이 위치를 목표로 합니다.
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = target.position + rotation * new Vector3(0f, 0f, -Radius);

        // 3. 현재 위치에서 목표 위치까지 부드럽게 이동합니다. (프레임 속도와 무관한 보간)
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, t);

        // 4. 카메라가 항상 대상을 바라보게 합니다.
        transform.LookAt(target);
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

    private float ReadMouseY()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue().y * 0.1f : 0f;
#else
        return Input.GetAxis("Mouse Y");
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
}