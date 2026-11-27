using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
public class GuardAI : MonoBehaviour
{
    public enum GuardState { Patrol, Suspicion, Chase, Return }

    [Header("대상")]
    [Tooltip("비워두면 Player 태그 오브젝트를 자동으로 찾습니다.")]
    [SerializeField] private Transform player;

    [Header("순찰")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waitTimeAtPoint = 1.5f;
    [SerializeField] private float arriveThreshold = 0.3f;

    [Header("감지")]
    [SerializeField] private float viewDistance = 10f;
    [Range(10f, 360f)]
    [SerializeField] private float viewAngle = 90f;
    [Tooltip("경비의 눈 높이 (Raycast 시작점)")]
    [SerializeField] private float eyeHeight = 1f;
    [Tooltip("플레이어의 어느 높이를 조준할지")]
    [SerializeField] private float targetHeight = 0.5f;

    [Header("의심")]
    [Tooltip("이 시간 동안 계속 보이면 추격으로 전환")]
    [SerializeField] private float suspicionTime = 1.5f;
    [SerializeField] private float turnSpeed = 5f;

    [Header("추격")]
    [SerializeField] private float chaseSpeed = 4f;
    [Tooltip("시야에서 놓친 뒤 이 시간이 지나면 복귀")]
    [SerializeField] private float loseSightTime = 3f;
    [Tooltip("이 거리 안으로 들어오면 붙잡은 것으로 처리 (NavMeshAgent Stopping Distance보다 크게)")]
    [SerializeField] private float catchDistance = 1.5f;

    [Header("이벤트")]
    [Tooltip("플레이어를 붙잡았을 때 호출됩니다. 나중에 GameManager의 실패 처리를 연결합니다.")]
    public UnityEvent onPlayerCaught;

    [Header("디버그")]
    [SerializeField] private bool logStateChange = true;

    public GuardState CurrentState { get; private set; }

    private NavMeshAgent agent;
    private int patrolIndex;
    private bool isWaiting;
    private float waitTimer;
    private float suspicionTimer;
    private float loseTimer;
    private Vector3 lastKnownPosition;
    private bool hasCaught;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogWarning("[GuardAI] Player 태그 오브젝트를 찾지 못했습니다.", this);
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
            Debug.LogWarning("[GuardAI] 순찰 지점이 없습니다.", this);

        EnterState(GuardState.Patrol);
    }

    private void Update()
    {
        if (hasCaught || player == null) return;

        bool seen = CanSeePlayer();

        switch (CurrentState)
        {
            case GuardState.Patrol:
                if (seen) { ChangeState(GuardState.Suspicion); return; }
                UpdatePatrol();
                break;

            case GuardState.Suspicion:
                UpdateSuspicion(seen);
                break;

            case GuardState.Chase:
                UpdateChase(seen);
                break;

            case GuardState.Return:
                if (seen) { ChangeState(GuardState.Suspicion); return; }
                if (HasArrived()) ChangeState(GuardState.Patrol);
                break;
        }
    }

    // ───────────── 상태별 동작 ─────────────

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (!HasArrived()) return;

        if (!isWaiting)
        {
            isWaiting = true;
            waitTimer = 0f;
        }

        waitTimer += Time.deltaTime;
        if (waitTimer >= waitTimeAtPoint)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            isWaiting = false;
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
    }

    private void UpdateSuspicion(bool seen)
    {
        if (seen)
        {
            suspicionTimer += Time.deltaTime;
            FaceTarget(player.position);

            if (suspicionTimer >= suspicionTime)
                ChangeState(GuardState.Chase);
        }
        else
        {
            suspicionTimer -= Time.deltaTime;
            if (suspicionTimer <= 0f)
                ChangeState(GuardState.Return);
        }
    }

    private void UpdateChase(bool seen)
    {
        if (seen)
        {
            loseTimer = 0f;
            lastKnownPosition = player.position;
        }
        else
        {
            loseTimer += Time.deltaTime;
            if (loseTimer >= loseSightTime)
            {
                ChangeState(GuardState.Return);
                return;
            }
        }

        agent.SetDestination(lastKnownPosition);

        // 높이 차이를 무시한 수평 거리로 붙잡음 판정
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude <= catchDistance)
            CatchPlayer();
    }

    // ───────────── 상태 전환 ─────────────

    private void ChangeState(GuardState next)
    {
        if (CurrentState == next) return;
        EnterState(next);
    }

    private void EnterState(GuardState next)
    {
        CurrentState = next;
        if (logStateChange) Debug.Log($"[GuardAI] {name} 상태: {next}", this);

        switch (next)
        {
            case GuardState.Patrol:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                isWaiting = false;
                if (patrolPoints != null && patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[patrolIndex].position);
                break;

            case GuardState.Suspicion:
                agent.isStopped = true;
                suspicionTimer = 0f;
                break;

            case GuardState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                loseTimer = 0f;
                lastKnownPosition = player.position;
                break;

            case GuardState.Return:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                if (patrolPoints != null && patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[patrolIndex].position);
                break;
        }
    }

    private void CatchPlayer()
    {
        hasCaught = true;
        agent.isStopped = true;
        if (logStateChange) Debug.Log("[GuardAI] 플레이어를 붙잡았습니다!", this);
        onPlayerCaught?.Invoke();
    }

    // ───────────── 감지 ─────────────

    private bool CanSeePlayer()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * targetHeight;
        Vector3 toTarget = target - eye;

        // 1) 거리
        if (toTarget.magnitude > viewDistance) return false;

        // 2) 시야각 (수평 기준)
        Vector3 flat = toTarget;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f)
            return false;

        // 3) 장애물 가림 (Raycast로 처음 맞은 것이 플레이어여야 함)
        if (Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit,
                viewDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == player || hit.transform.IsChildOf(player);
        }
        return false;
    }

    // ───────────── 보조 ─────────────

    private bool HasArrived()
    {
        return !agent.pathPending && agent.remainingDistance <= arriveThreshold;
    }

    private void FaceTarget(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(eye, eye + left * viewDistance);
        Gizmos.DrawLine(eye, eye + right * viewDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
    }
}