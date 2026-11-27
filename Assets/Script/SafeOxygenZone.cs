using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SafeOxygenZone : MonoBehaviour
{
    [Tooltip("비워두면 씬에서 RabbitOxygenSystem을 자동으로 찾습니다.")]
    [SerializeField] private RabbitOxygenSystem oxygenSystem;

    private bool playerInside;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        if (oxygenSystem == null)
            oxygenSystem = FindFirstObjectByType<RabbitOxygenSystem>();
        if (oxygenSystem == null)
            Debug.LogWarning("[SafeOxygenZone] RabbitOxygenSystem을 찾지 못했습니다.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oxygenSystem == null || playerInside || !other.CompareTag("Player")) return;
        playerInside = true;
        oxygenSystem.EnterSafeZone();
    }

    private void OnTriggerExit(Collider other)
    {
        if (oxygenSystem == null || !playerInside || !other.CompareTag("Player")) return;
        playerInside = false;
        oxygenSystem.ExitSafeZone();
    }

    private void OnDisable()
    {
        // 구역이 꺼질 때 플레이어가 안에 있었다면 카운트를 되돌립니다.
        if (playerInside && oxygenSystem != null)
        {
            playerInside = false;
            oxygenSystem.ExitSafeZone();
        }
    }
}