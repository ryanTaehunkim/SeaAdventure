using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitGoal : MonoBehaviour
{
    [Tooltip("비워두면 씬에서 GameManager를 자동으로 찾습니다.")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private string needRabbitMessage = "토끼를 구출해야 탈출할 수 있어요!";

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null)
            Debug.LogWarning("[ExitGoal] GameManager를 찾지 못했습니다.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (gameManager == null || !other.CompareTag("Player")) return;

        PlayerController2 pc = other.GetComponentInParent<PlayerController2>();
        if (pc == null) return;

        if (pc.HasRabbit) gameManager.WinGame();
        else gameManager.ShowMessage(needRabbitMessage);
    }

    private void OnTriggerStay(Collider other)
    {
        if (gameManager == null || !other.CompareTag("Player")) return;

        PlayerController2 pc = other.GetComponentInParent<PlayerController2>();
        if (pc != null && pc.HasRabbit) gameManager.WinGame();
    }
}