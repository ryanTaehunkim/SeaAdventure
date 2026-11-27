using UnityEngine;

[RequireComponent(typeof(Collider))]
public class OxygenBubble : MonoBehaviour
{
    [Tooltip("비워두면 씬에서 RabbitOxygenSystem을 자동으로 찾습니다.")]
    [SerializeField] private RabbitOxygenSystem oxygenSystem;
    [SerializeField] private float restoreAmount = 25f;
    [Tooltip("체크하면 산소가 감소 중일 때(토끼 구출 후)만 획득됩니다.")]
    [SerializeField] private bool onlyWhileDraining = true;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        if (oxygenSystem == null)
            oxygenSystem = FindFirstObjectByType<RabbitOxygenSystem>();
        if (oxygenSystem == null)
            Debug.LogWarning("[OxygenBubble] RabbitOxygenSystem을 찾지 못했습니다.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oxygenSystem == null || !other.CompareTag("Player")) return;
        if (onlyWhileDraining && !oxygenSystem.IsDraining) return;

        oxygenSystem.Restore(restoreAmount);
        Destroy(gameObject);
    }
}