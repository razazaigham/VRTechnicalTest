using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour, IDamageable
{
    public enum State
    {
        Idle,
        Detect,
        Approach,
        Attack,
        Hurt,
        Dead
    }

    public float maxHealth = 100f;
    public float bodyDamage = 34f;
    public float headDamage = 100f;
    public float detectRange = 13f;
    public float attackRange = 1.55f;
    public float moveSpeed = 2.35f;
    public float attackDamage = 8f;
    public float attackInterval = 1.05f;

    public State CurrentState { get; private set; } = State.Idle;
    public bool IsAlive => CurrentState != State.Dead;

    float health;
    float stateUntil;
    float nextAttackTime;
    float nextDestinationTime;
    NavMeshAgent agent;
    Transform player;
    Renderer[] renderers;
    Color[] baseColors;
    Vector3 idleOrigin;
    Vector3 moveTarget;
    EnemyHitbox[] hitboxes;
    float nextLosTime;
    bool cachedLos = true;

    public static GameObject CreateTemplate()
    {
        var root = new GameObject("Enemy");
        root.AddComponent<NavMeshAgent>();
        var enemy = root.AddComponent<Enemy>();
        enemy.BuildVisuals();
        root.SetActive(false);
        return root;
    }

    public void Activate(Vector3 worldPosition)
    {
        health = maxHealth;
        CurrentState = State.Idle;
        idleOrigin = worldPosition;
        stateUntil = Time.time + Random.Range(0.6f, 2.2f);
        nextAttackTime = 0f;
        RestoreColors();
        SetHitboxes(true);

        transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        gameObject.SetActive(true);

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.speed = moveSpeed;
            agent.angularSpeed = 420f;
            agent.acceleration = 10f;
            agent.stoppingDistance = attackRange * 0.85f;
            agent.radius = 0.32f;
            agent.height = 1.8f;
            agent.obstacleAvoidanceType = XrPerformance.Enabled
                ? ObstacleAvoidanceType.LowQualityObstacleAvoidance
                : ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = maxHealth;
        CacheRenderers();
    }

    void Update()
    {
        if (CurrentState == State.Dead)
            return;

        ResolvePlayer();
        switch (CurrentState)
        {
            case State.Idle:
                TickIdle();
                break;
            case State.Detect:
                TickDetect();
                break;
            case State.Approach:
                TickApproach();
                break;
            case State.Attack:
                TickAttack();
                break;
            case State.Hurt:
                if (Time.time >= stateUntil)
                    Enter(CanSeePlayer() ? State.Approach : State.Idle);
                break;
        }
    }

    public void TakeDamage(float amount, bool isHeadshot, RaycastHit hit)
    {
        if (!IsAlive)
            return;

        health -= amount;
        Flash(isHeadshot ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.55f, 0.2f));
        AlertNearby();

        if (health <= 0f)
        {
            Die(isHeadshot);
            return;
        }

        Enter(State.Hurt);
        stateUntil = Time.time + 0.22f;
        if (agent != null && agent.enabled)
            agent.isStopped = true;
    }

    void TickIdle()
    {
        if (CanSeePlayer() || DistanceToPlayer() <= detectRange * 0.45f)
        {
            Enter(State.Detect);
            return;
        }

        if (Time.time >= nextDestinationTime)
        {
            nextDestinationTime = Time.time + Random.Range(1.4f, 3.2f);
            Vector3 wander = idleOrigin + Random.insideUnitSphere * 1.6f;
            wander.y = transform.position.y;
            MoveTo(wander);
        }

        StepFallback();
    }

    void TickDetect()
    {
        FacePlayer();
        if (Time.time >= stateUntil)
            Enter(State.Approach);
    }

    void TickApproach()
    {
        if (DistanceToPlayer() <= attackRange)
        {
            Enter(State.Attack);
            return;
        }

        if (!CanSeePlayer() && DistanceToPlayer() > detectRange * 1.25f)
        {
            Enter(State.Idle);
            return;
        }

        if (Time.time >= nextDestinationTime)
        {
            nextDestinationTime = Time.time + 0.25f;
            MoveTo(PlayerPosition());
        }

        StepFallback();
    }

    void TickAttack()
    {
        float dist = DistanceToPlayer();
        if (dist > attackRange * 1.25f)
        {
            Enter(State.Approach);
            return;
        }

        FacePlayer();
        if (agent != null && agent.enabled)
            agent.isStopped = true;

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackInterval;
            transform.position += transform.forward * 0.08f;
            if (PlayerHealth.Instance != null)
                PlayerHealth.Instance.TakeDamage(attackDamage);
        }
    }

    void Enter(State next)
    {
        CurrentState = next;
        if (agent != null && agent.enabled)
            agent.isStopped = next == State.Attack || next == State.Hurt || next == State.Dead;

        if (next == State.Detect)
            stateUntil = Time.time + 0.35f;
    }

    void Die(bool headshot)
    {
        CurrentState = State.Dead;
        SetHitboxes(false);
        if (agent != null)
            agent.enabled = false;

        transform.rotation *= Quaternion.Euler(headshot ? 80f : 70f, 0f, 12f);
        EnemyWaveManager.Instance?.NotifyEnemyDied(this);
        Destroy(gameObject, 3.5f);
    }

    public void Alert()
    {
        if (!IsAlive || CurrentState == State.Approach || CurrentState == State.Attack)
            return;
        Enter(State.Detect);
    }

    void AlertNearby()
    {
        Alert();
        var enemies = EnemyWaveManager.Instance != null ? EnemyWaveManager.Instance.LiveEnemies : null;
        if (enemies == null)
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy other = enemies[i];
            if (other == null || other == this || !other.IsAlive)
                continue;
            if (Vector3.Distance(transform.position, other.transform.position) <= 8f)
                other.Alert();
        }
    }

    void MoveTo(Vector3 destination)
    {
        moveTarget = destination;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
    }

    void StepFallback()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            return;

        Vector3 flat = moveTarget;
        flat.y = transform.position.y;
        if ((flat - transform.position).sqrMagnitude < 0.01f)
            return;

        transform.position = Vector3.MoveTowards(transform.position, flat, moveSpeed * Time.deltaTime);
        Face(flat);
    }

    void FacePlayer()
    {
        Face(PlayerPosition());
    }

    void Face(Vector3 worldPoint)
    {
        Vector3 dir = worldPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    bool CanSeePlayer()
    {
        if (XrPerformance.Enabled && Time.time < nextLosTime)
            return cachedLos;

        if (XrPerformance.Enabled)
            nextLosTime = Time.time + 0.2f;
        Vector3 origin = transform.position + Vector3.up * 1.65f + transform.forward * 0.15f;
        Vector3 target = PlayerPosition() + Vector3.up * 1.2f;
        Vector3 to = target - origin;
        if (to.magnitude > detectRange)
        {
            cachedLos = false;
            return false;
        }

        if (Vector3.Angle(transform.forward, to) > 110f)
        {
            cachedLos = false;
            return false;
        }

        if (Physics.Raycast(origin, to.normalized, out RaycastHit hit, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            cachedLos = (player != null && hit.transform != null && hit.transform.IsChildOf(player))
                        || (hit.collider != null && hit.collider.GetComponentInParent<PlayerHealth>() != null);
            return cachedLos;
        }

        cachedLos = true;
        return true;
    }

    float DistanceToPlayer()
    {
        Vector3 a = transform.position;
        Vector3 b = PlayerPosition();
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    Vector3 PlayerPosition()
    {
        return player != null ? player.position : Vector3.zero;
    }

    void ResolvePlayer()
    {
        if (player != null)
            return;
        if (Camera.main != null)
            player = Camera.main.transform;
    }

    void Flash(Color color)
    {
        if (renderers == null)
            return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].material.color = color;
            renderers[i].material.SetColor("_BaseColor", color);
        }
        Invoke(nameof(RestoreColors), 0.12f);
    }

    void RestoreColors()
    {
        if (renderers == null)
            return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].material.color = baseColors[i];
            renderers[i].material.SetColor("_BaseColor", baseColors[i]);
        }
    }

    void SetHitboxes(bool enabled)
    {
        if (hitboxes == null)
            hitboxes = GetComponentsInChildren<EnemyHitbox>(true);
        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] != null)
                hitboxes[i].GetComponent<Collider>().enabled = enabled;
        }
    }

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].sharedMaterial != null ? renderers[i].sharedMaterial.color : Color.white;
    }

    void BuildVisuals()
    {
        Material bodyMat = ArenaBuilder.MakeMaterial(new Color(0.42f, 0.18f, 0.14f));
        Material headMat = ArenaBuilder.MakeMaterial(new Color(0.82f, 0.64f, 0.48f));
        Material darkMat = ArenaBuilder.MakeMaterial(new Color(0.08f, 0.08f, 0.1f));
        Material pantsMat = ArenaBuilder.MakeMaterial(new Color(0.18f, 0.2f, 0.24f));

        var body = CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.42f, 0.7f, 0.42f), bodyMat);
        var bodyHit = body.AddComponent<EnemyHitbox>();
        bodyHit.enemy = this;
        bodyHit.isHead = false;

        var legs = CreatePart(PrimitiveType.Capsule, "Legs", new Vector3(0f, 0.38f, 0f), new Vector3(0.38f, 0.38f, 0.38f), pantsMat);
        Object.DestroyImmediate(legs.GetComponent<Collider>());

        var head = CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.62f, 0.02f), Vector3.one * 0.28f, headMat);
        var headHit = head.AddComponent<EnemyHitbox>();
        headHit.enemy = this;
        headHit.isHead = true;

        var visor = CreatePart(PrimitiveType.Cube, "Visor", new Vector3(0f, 1.64f, 0.12f), new Vector3(0.18f, 0.06f, 0.04f), darkMat);
        Object.DestroyImmediate(visor.GetComponent<Collider>());

        var armL = CreatePart(PrimitiveType.Cube, "ArmL", new Vector3(-0.28f, 0.95f, 0.04f), new Vector3(0.1f, 0.46f, 0.12f), bodyMat);
        var armR = CreatePart(PrimitiveType.Cube, "ArmR", new Vector3(0.28f, 0.95f, 0.04f), new Vector3(0.1f, 0.46f, 0.12f), bodyMat);
        Object.DestroyImmediate(armL.GetComponent<Collider>());
        Object.DestroyImmediate(armR.GetComponent<Collider>());

        CacheRenderers();
        hitboxes = GetComponentsInChildren<EnemyHitbox>(true);
    }

    GameObject CreatePart(PrimitiveType type, string partName, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
