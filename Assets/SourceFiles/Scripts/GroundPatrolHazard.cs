using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Moves between patrol points or chases the player, and damages the player when a contact sphere overlaps the Player.
/// Uses <see cref="CharacterController"/> for locomotion so the hazard collides with static geometry (no ghosting through walls).
/// Patrol priority: <see cref="worldPatrolPoints"/> (absolute), then <see cref="waypoints"/> (scene transforms; do not parent under this object),
/// then <see cref="patrolOffsetsFromSpawn"/> relative to position at <see cref="Awake"/>.
/// When <see cref="chasePlayer"/> is on, the hazard follows the player on the XZ plane (same vertical position as this object).
/// Optional <see cref="preventFallingOffEdges"/> raycasts before horizontal motion so the hazard does not walk into the void or off tall ledges.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(CharacterController))]
public class GroundPatrolHazard : MonoBehaviour
{
    [Header("Chase")]
    [Tooltip("If true, moves toward the Player on XZ (ignores patrol until the player is missing).")]
    [SerializeField] bool chasePlayer = true;

    [Tooltip("Child with SkinnedMeshRenderer (e.g. Steel Sentinel), used for foot height when snapping to ground.")]
    [SerializeField] string visualChildName = "SteelSentinel_Visual";

    [Tooltip("Degrees added to chase facing (use 180 if the model faces backward).")]
    [SerializeField] float chaseFacingYawOffset = 0f;

    [Header("Patrol")]
    [Tooltip("If length > 0, these absolute world positions are used.")]
    [SerializeField] Vector3[] worldPatrolPoints;

    [SerializeField] Transform[] waypoints;

    [Tooltip("Used when world points and transforms are empty; XZ loop relative to position at Awake.")]
    [SerializeField] Vector3[] patrolOffsetsFromSpawn = { new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 5f), new Vector3(0f, 0f, 5f) };
    [SerializeField] float moveSpeed = 1.25f;
    [SerializeField] float reachThreshold = 0.08f;
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] float damageCooldown = 1.25f;

    [Tooltip("After dealing damage to the player, the hazard stops moving and chasing for this many seconds.")]
    [SerializeField] float pauseAfterSuccessfulHitSeconds = 2f;

    [Header("Animation")]
    [Tooltip("Walk cycle from the merged animations .glb (assigned by Tools → Bad Guy → Integrate / Repair). Uses a PlayableGraph so the walk plays even if the Animator Controller has no motion.")]
    [SerializeField] AnimationClip walkClip;

    [Tooltip("Scales walk clip playback vs actual horizontal speed (keeps walk visible while chasing).")]
    [SerializeField] bool syncWalkAnimatorSpeed = true;

    [SerializeField] float walkAnimatorSpeedMin = 0.5f;

    [Header("Ground snap")]
    [Tooltip("Raycast down on startup to place the character on collider geometry (fixes floating above the floor).")]
    [SerializeField] bool snapFeetToGroundOnStart = true;

    [SerializeField] LayerMask groundSnapMask = Physics.DefaultRaycastLayers;

    [Tooltip("Extra meters above the highest renderer used as the ray start (avoids hitting our own colliders first).")]
    [SerializeField] float snapSkyClearanceAboveHead = 300f;

    [Tooltip("Fine-tune after snap: positive pushes the root up, negative down (meters).")]
    [SerializeField] float groundSnapVerticalOffset;

    [Tooltip("Frames to keep re-snapping after load (animator/playable/bounds keep changing).")]
    [SerializeField] int groundSnapCoroutineIterations = 40;

    [Header("Edge safety")]
    [Tooltip("Raycast ahead of horizontal motion; block movement into open air or drops larger than max step down.")]
    [SerializeField] bool preventFallingOffEdges = true;

    [Tooltip("Layers treated as walkable ground for edge probes (same idea as ground snap).")]
    [SerializeField] LayerMask edgeSafetyGroundMask = Physics.DefaultRaycastLayers;

    [Tooltip("Meters above current feet height to start the downward ray at the next XZ position.")]
    [SerializeField] float edgeProbeStartAboveFeet = 2.5f;

    [Tooltip("Max vertical drop to the surface under the next step (meters). Larger than this counts as a cliff. Use ~2+ so the hazard can step off props and stairs; CharacterController still blocks walking into walls.")]
    [SerializeField] float maxStepDownForEdgeSafety = 2.5f;

    [Header("Player contact")]
    [Tooltip("Sphere radius (meters) at playerContactOffsetLocal for detecting the Player (replaces trigger volume).")]
    [SerializeField] float playerContactRadius = 1.1f;

    [Tooltip("Local offset from this transform for the contact sphere center (roughly torso height).")]
    [SerializeField] Vector3 playerContactOffsetLocal = new Vector3(0f, 1f, 0f);

    int _waypointIndex;
    float _nextDamageTime;
    float _resumeMoveAfterHitTime = float.NegativeInfinity;
    Vector3 _spawnOrigin;
    Vector3 _positionLastFrame;
    Transform _playerTransform;
    Animator _animator;
    PlayableGraph _walkPlayableGraph;
    AnimationClipPlayable _walkClipPlayable;
    bool _walkPlayableActive;

    CharacterController _cc;
    Vector3 _verticalVelocity;
    readonly Collider[] _contactOverlapBuffer = new Collider[16];

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _spawnOrigin = transform.position;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
        {
            _playerTransform = playerGo.transform;
            if (playerHealth == null)
                playerHealth = playerGo.GetComponent<PlayerHealth>();
        }
    }

    void Start()
    {
        _positionLastFrame = transform.position;
        _animator = GetComponentInChildren<Animator>();
        TryStartWalkPlayable();

        if (_animator != null)
            _animator.Update(0.02f);
        Physics.SyncTransforms();

        if (snapFeetToGroundOnStart)
            StartCoroutine(CoGroundSnap());
    }

    void OnDestroy()
    {
        if (_walkPlayableGraph.IsValid())
            _walkPlayableGraph.Destroy();
    }

    void TryStartWalkPlayable()
    {
        if (walkClip == null || _animator == null)
            return;

        // Drive the rig directly; empty/broken Animator Controllers show no motion.
        _animator.runtimeAnimatorController = null;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        _walkPlayableGraph = PlayableGraph.Create(gameObject.name + "_BadGuyWalk");
        _walkClipPlayable = AnimationClipPlayable.Create(_walkPlayableGraph, walkClip);
        _walkClipPlayable.SetApplyFootIK(false);
        _walkClipPlayable.SetSpeed(1f);

        var output = AnimationPlayableOutput.Create(_walkPlayableGraph, "Animation", _animator);
        output.SetSourcePlayable(_walkClipPlayable);
        _walkPlayableGraph.Play();
        _walkPlayableActive = true;
    }

    void SetWalkPlaybackSpeed(float speed)
    {
        if (_walkPlayableActive && _walkClipPlayable.IsValid())
            _walkClipPlayable.SetSpeed(speed);
        else if (_animator != null)
            _animator.speed = speed;
    }

    IEnumerator CoGroundSnap()
    {
        yield return null;
        yield return null;

        for (var i = 0; i < groundSnapCoroutineIterations; i++)
        {
            if (!snapFeetToGroundOnStart)
                yield break;

            TrySnapCharacterFeetToGroundBelow();
            yield return null;
        }
    }

    void Update()
    {
        if (Time.time < _resumeMoveAfterHitTime)
        {
            SetWalkPlaybackSpeed(0f);
            _positionLastFrame = transform.position;
            return;
        }

        if (!syncWalkAnimatorSpeed)
            SetWalkPlaybackSpeed(1f);

        if (TryGetMoveTarget(out var targetPos, out var count) && count > 0)
        {
            var step = moveSpeed * Time.deltaTime;
            var current = transform.position;
            var next = Vector3.MoveTowards(current, targetPos, step);

            if (_cc != null)
            {
                var horizontal = next - current;
                horizontal.y = 0f;

                if (preventFallingOffEdges && horizontal.sqrMagnitude > 1e-8f &&
                    !IsHorizontalMoveSafe(current, horizontal))
                    horizontal = Vector3.zero;

                if (_cc.isGrounded && _verticalVelocity.y < 0f)
                    _verticalVelocity.y = -2f;
                else
                    _verticalVelocity.y += Physics.gravity.y * Time.deltaTime;

                var move = horizontal + Vector3.up * (_verticalVelocity.y * Time.deltaTime);
                _cc.Move(move);
            }
            else
            {
                transform.position = next;
            }

            if (chasePlayer && _playerTransform != null)
            {
                var flat = _playerTransform.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                {
                    var look = Quaternion.LookRotation(flat.normalized);
                    transform.rotation = Quaternion.Euler(0f, look.eulerAngles.y + chaseFacingYawOffset, 0f);
                }
            }
            else if (Vector3.Distance(transform.position, targetPos) <= reachThreshold)
            {
                _waypointIndex = (_waypointIndex + 1) % count;
            }

            if (syncWalkAnimatorSpeed && (_walkPlayableActive || _animator != null))
            {
                var flatDelta = transform.position - _positionLastFrame;
                flatDelta.y = 0f;
                var v = flatDelta.magnitude / Time.deltaTime;
                var t = Mathf.Clamp01(v / Mathf.Max(moveSpeed, 0.01f));
                SetWalkPlaybackSpeed(Mathf.Lerp(walkAnimatorSpeedMin, 1f, t));
            }

            _positionLastFrame = transform.position;
        }

        TryDamagePlayerIfOverlapping();
    }

    /// <summary>
    /// Drops the root so the controller's world AABB bottom meets the first solid surface below a sky ray.
    /// Ray origin height uses the highest enabled <see cref="Renderer"/> under <see cref="visualChildName"/> when that child exists; otherwise all renderers under this transform.
    /// </summary>
    bool TrySnapCharacterFeetToGroundBelow()
    {
        Physics.SyncTransforms();

        var p = transform.position;
        var highest = p.y;
        var scanRoot = transform;
        if (!string.IsNullOrEmpty(visualChildName))
        {
            var vis = transform.Find(visualChildName);
            if (vis != null)
                scanRoot = vis;
        }

        foreach (var r in scanRoot.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled)
                continue;
            highest = Mathf.Max(highest, r.bounds.max.y);
        }

        // Start far above the mesh so the first meaningful hit is the floor, not a stray self-collider mid-body.
        var originY = Mathf.Max(highest + snapSkyClearanceAboveHead, p.y + snapSkyClearanceAboveHead);
        var origin = new Vector3(p.x, originY, p.z);
        var rayLength = originY + 4096f;

        if (!TryFindGroundPointBelow(origin, rayLength, groundSnapMask, out var groundY))
        {
            // Layer mask too restrictive (e.g. ground on a layer not in DefaultRaycastLayers)
            if (!TryFindGroundPointBelow(origin, rayLength, -1, out groundY))
                return false;
        }

        var lowest = GetControllerBottomWorldY();
        var delta = groundY - lowest + groundSnapVerticalOffset;
        if (Mathf.Abs(delta) <= 0.0005f)
            return true;

        ApplyRootWorldOffset(Vector3.up * delta);
        return true;
    }

    static bool IsColliderUnderOurHierarchy(Transform root, Collider c)
    {
        if (c == null)
            return true;
        var t = c.transform;
        return t == root || t.IsChildOf(root);
    }

    bool TryFindGroundPointBelow(Vector3 origin, float maxDistance, int layerMask, out float groundY)
    {
        groundY = 0f;
        var hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null)
                continue;
            if (IsColliderUnderOurHierarchy(transform, h.collider))
                continue;
            if (h.collider.CompareTag("Player"))
                continue;

            groundY = h.point.y;
            return true;
        }

        return false;
    }

    void ApplyRootWorldOffset(Vector3 worldDelta)
    {
        if (_cc != null)
        {
            _cc.enabled = false;
            transform.position += worldDelta;
            Physics.SyncTransforms();
            _cc.enabled = true;
            _verticalVelocity.y = 0f;
        }
        else
        {
            transform.position += worldDelta;
        }
    }

    /// <summary>
    /// World-space bottom of the hazard's <see cref="CharacterController"/> AABB (stable; matches gameplay volume).
    /// </summary>
    float GetControllerBottomWorldY()
    {
        Physics.SyncTransforms();
        if (_cc != null && _cc.enabled)
            return _cc.bounds.min.y;

        return transform.position.y;
    }

    /// <summary>
    /// Returns false only for open air or a large drop (true cliff). Does not reject walking toward vertical
    /// obstacles — the first ray hit might be a crate top above the feet; <see cref="CharacterController"/> handles collision.
    /// </summary>
    bool IsHorizontalMoveSafe(Vector3 fromWorld, Vector3 horizontalDelta)
    {
        if (horizontalDelta.sqrMagnitude < 1e-12f)
            return true;

        var next = fromWorld + horizontalDelta;
        var feetY = GetControllerBottomWorldY();
        var originY = feetY + edgeProbeStartAboveFeet;
        var origin = new Vector3(next.x, originY, next.z);
        var maxDist = originY + 4096f;

        if (!TryFindGroundPointBelow(origin, maxDist, edgeSafetyGroundMask, out var groundY))
        {
            if (!TryFindGroundPointBelow(origin, maxDist, -1, out groundY))
                return false;
        }

        // Large fall only: stepping off boxes/stairs uses a higher default max step down than CharacterController step.
        if (groundY < feetY - maxStepDownForEdgeSafety)
            return false;

        return true;
    }

    void TryDamagePlayerIfOverlapping()
    {
        var center = transform.TransformPoint(playerContactOffsetLocal);
        var n = Physics.OverlapSphereNonAlloc(
            center,
            playerContactRadius,
            _contactOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        for (var i = 0; i < n; i++)
        {
            var c = _contactOverlapBuffer[i];
            if (c != null && c.gameObject.CompareTag("Player"))
            {
                TryDamagePlayer();
                return;
            }
        }
    }

    bool TryGetMoveTarget(out Vector3 position, out int count)
    {
        if (chasePlayer && _playerTransform != null)
        {
            position = _playerTransform.position;
            position.y = transform.position.y;
            count = 1;
            return true;
        }

        return TryGetTargetPosition(out position, out count);
    }

    bool TryGetTargetPosition(out Vector3 position, out int count)
    {
        position = default;
        count = 0;

        if (worldPatrolPoints != null && worldPatrolPoints.Length > 0)
        {
            count = worldPatrolPoints.Length;
            position = worldPatrolPoints[_waypointIndex % count];
            return true;
        }

        if (waypoints != null && waypoints.Length > 0)
        {
            count = waypoints.Length;
            var t = waypoints[_waypointIndex % count];
            if (t == null)
                return false;
            position = t.position;
            return true;
        }

        if (patrolOffsetsFromSpawn != null && patrolOffsetsFromSpawn.Length > 0)
        {
            count = patrolOffsetsFromSpawn.Length;
            position = _spawnOrigin + patrolOffsetsFromSpawn[_waypointIndex % count];
            return true;
        }

        return false;
    }

    void TryDamagePlayer()
    {
        if (Time.time < _resumeMoveAfterHitTime)
            return;

        if (playerHealth == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerHealth = p.GetComponent<PlayerHealth>();
        }

        if (playerHealth == null || playerHealth.CurrentHealth <= 0)
            return;

        if (Time.time < _nextDamageTime)
            return;

        playerHealth.TakeDamage(1);
        if (playerHealth.TryGetComponent<RobotDamageHitFlash>(out var hitFlash))
            hitFlash.PlayHitFlash();
        _nextDamageTime = Time.time + damageCooldown;
        _resumeMoveAfterHitTime = Time.time + pauseAfterSuccessfulHitSeconds;
    }
}
