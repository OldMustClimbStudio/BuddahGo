using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

public class SkillExecutor : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private ComboSkillInput comboInput;
    [SerializeField] private SkillLoadout loadout;
    [SerializeField] private SkillDatabase database;
    private float _castLockedUntil;

    

    private float[] _nextReadyTime; // server cooldown tracking

    /// <summary> Cached reference. </summary>
    private ObsessionFigure _obs;

    private void Awake()
    {
        _nextReadyTime = new float[SkillLoadout.SlotCount];
        ResolveObsessionFigure();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveObsessionFigure();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner) return;

        if (comboInput == null) comboInput = GetComponent<ComboSkillInput>();
        if (loadout == null) loadout = GetComponent<SkillLoadout>();

        if (comboInput != null)
        {
            comboInput.OnSkillSlotTriggered += OnSlotTriggeredByCombo;
        }
        else
        {
            Debug.LogWarning("[SkillExecutor] Missing ComboSkillInput reference.");
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (comboInput != null)
            comboInput.OnSkillSlotTriggered -= OnSlotTriggeredByCombo;
    }

    private void OnSlotTriggeredByCombo(int slotIndex, string comboName)
    {
        // Runs only on the owning client.
        Debug.Log($"[SkillExecutor][Owner] Combo '{comboName}' triggered slot {slotIndex}, requesting cast...");
        RequestCast(slotIndex);
    }

    public void RequestCast(int slotIndex)
    {
        if (!IsOwner) return;
        CastSlotServerRpc(slotIndex);
    }

    [ServerRpc(RequireOwnership = true)]
    private void CastSlotServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SkillLoadout.SlotCount) return;
        if (loadout == null || database == null)
        {
            Debug.LogWarning("[SkillExecutor][Server] Missing loadout/database.");
            return;
        }

        string skillId = loadout.GetSkillId(slotIndex);
        if (string.IsNullOrWhiteSpace(skillId))
        {
            Debug.Log($"[SkillExecutor][Server] Slot {slotIndex} is empty, cast ignored.");
            return;
        }

        if (!database.TryGet(skillId, out SkillAction skill) || skill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Unknown skillId '{skillId}' (slot {slotIndex}).");
            return;
        }

        float now = (float)Time.time;
        if (now < _castLockedUntil) return;

        if (now < _nextReadyTime[slotIndex])
        {
            Debug.Log($"[SkillExecutor][Server] Skill '{skillId}' on cooldown. Ready in {(_nextReadyTime[slotIndex] - now):0.00}s");
            return;
        }

        // Enter cooldown after validation succeeds.
        _castLockedUntil = now + skill.castLockSeconds;
        _nextReadyTime[slotIndex] = now + skill.cooldownSeconds;

        Debug.Log($"[SkillExecutor][Server] CAST '{skillId}' (slot {slotIndex})");

        ResolveObsessionFigure();
        float obsessionNow = (_obs != null) ? _obs.Current : 0f;
        float backfirePercent = (_obs != null) ? _obs.GetBackfireProbabilityPercent(obsessionNow) : 0f;

        bool isAnti = false;
        SkillAction executedSkill = skill;
        string executedSkillId = skillId;

        if (skill.HasAnti && backfirePercent > 0.0001f)
        {
            float roll = Random.value * 100f;
            isAnti = roll < backfirePercent;

            if (isAnti)
            {
                if (database.TryGet(skill.antiSkillId, out SkillAction antiSkill) && antiSkill != null)
                {
                    executedSkill = antiSkill;
                    executedSkillId = skill.antiSkillId; // 用 DB key 保持一致
                }
                else
                {
                    Debug.LogWarning($"[SkillExecutor][Server] Anti skillId '{skill.antiSkillId}' not found. Fallback normal.");
                    isAnti = false;
                }
            }

            Debug.Log($"[SkillExecutor][Server] Backfire roll: obsession={obsessionNow:0.###}, p={backfirePercent:0.###}%, roll={roll:0.###} -> anti={(isAnti ? "YES" : "NO")}");
        }

        // cooldown/castLock 仍按原技能

        // 真正执行：普通 or 反噬
        executedSkill.ExecuteServer(this, slotIndex);

        // obsession 增长仍按原技能 obsessionGain
        _obs?.AddServer(Mathf.Max(0f, skill.ObsessionGain));

        // 广播给所有客户端播放正确版本
        CastObserversRpc(slotIndex, executedSkillId, isAnti);
    }

        [ObserversRpc]
        private void CastObserversRpc(int slotIndex, string executedSkillId, bool isAnti)
        {
            if (database == null) return;

            if (database.TryGet(executedSkillId, out SkillAction skill) && skill != null)
            {
                Debug.Log($"[SkillExecutor][Observers] '{executedSkillId}' played (slot {slotIndex}) [anti={isAnti}]");
                skill.ExecuteObservers(this, slotIndex);
            }
        }
    public void ApplyAccelerationToOwner(float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        if (!IsServerInitialized) return;

        NetworkConnection conn = Owner;
        if (conn == null) return;

        ApplyAccelerationTargetRpc(conn, extraForwardForce, extraMaxSpeed, durationSeconds);
    }

    [TargetRpc]
    private void ApplyAccelerationTargetRpc(NetworkConnection conn, float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        // Runs only on the owning client.
        var move = GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement.");
            return;
        }

        var effect = GetComponent<MovementAccelerationEffect>();
        if (effect == null)
            effect = gameObject.AddComponent<MovementAccelerationEffect>();

        effect.ApplyOrRefresh(move, extraForwardForce, extraMaxSpeed, durationSeconds);

        Debug.Log($"[SkillExecutor][Target] Accel: +{extraForwardForce} forwardForce, +{extraMaxSpeed} maxSpeed for {durationSeconds}s");
    }

    private void ResolveObsessionFigure()
    {
        if (_obs != null)
            return;

        _obs = GetComponent<ObsessionFigure>();
        if (_obs == null)
            _obs = GetComponentInParent<ObsessionFigure>();
        if (_obs == null)
            _obs = GetComponentInChildren<ObsessionFigure>(true);
    }

}
