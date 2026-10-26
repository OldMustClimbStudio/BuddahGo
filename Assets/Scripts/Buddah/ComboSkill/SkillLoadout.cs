using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SteamMultiplayer.Network;
using UnityEngine;

public class SkillLoadout : NetworkBehaviour
{
    public const int SlotCount = 3;

    [Header("Config Bridge")]
    [SerializeField] private SkillDatabase _skillDatabase;

    [Header("Default Skills")]
    [Tooltip("Legacy fallback. Config default loadout overrides this when available.")]
    [SerializeField] private string[] _defaultSkillIds = { "acceleration", "push_projectile_hands", "blackcurtain" };

    // slotIndex -> skillId
    public readonly SyncList<string> SlotSkillIds = new SyncList<string>();

    /// <summary>
    /// Server: the slots were (re)written by SetSlotsServer (also reached from TryApplyResolvedSelectionServer),
    /// SetSlotServer or ApplyDefaultSkillsServer. SkillExecutor re-registers the racer's 缘起 on it.
    /// </summary>
    public event Action ServerSlotsChanged;

    public override void OnStartServer()
    {
        base.OnStartServer();

        EnsureSlotCountServer();

        if (!TryApplyResolvedSelectionServer(Owner != null ? Owner.ClientId : -1))
            ApplyDefaultSkillsServer();
    }

    /// <summary>The current slot ids as a new array (empty strings until the slots exist).</summary>
    public string[] CopySlotSkillIds()
    {
        var ids = new string[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            ids[i] = GetSkillId(i);
        return ids;
    }

    public string GetSkillId(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return string.Empty;

        if (SlotSkillIds.Count < SlotCount)
            return string.Empty;

        return SlotSkillIds[slotIndex] ?? string.Empty;
    }

    [Server]
    public void SetSlotsServer(IReadOnlyList<string> skillIds)
    {
        EnsureSlotCountServer();

        for (int i = 0; i < SlotCount; i++)
        {
            string skillId = (skillIds != null && i < skillIds.Count ? skillIds[i] : string.Empty) ?? string.Empty;
            SlotSkillIds[i] = skillId;
        }

        LogFinalLoadout("SetSlotsServer");
        ServerSlotsChanged?.Invoke();
    }

    [Server]
    public void SetSlotServer(int slotIndex, string skillId)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return;

        EnsureSlotCountServer();
        SlotSkillIds[slotIndex] = skillId ?? string.Empty;
        GameLog.Verbose($"[SkillLoadout][Server] Set slot {slotIndex} -> '{SlotSkillIds[slotIndex]}'");
        ServerSlotsChanged?.Invoke();
    }

    [Server]
    public bool TryApplyResolvedSelectionServer(int playerId)
    {
        if (playerId < 0)
            return false;

        if (!ResolvedPropertySelectionCache.TryGetPlayerSkillLoadout(playerId, out string[] resolvedLoadout)
            || resolvedLoadout == null
            || resolvedLoadout.Length < SlotCount)
        {
            return false;
        }

        SetSlotsServer(resolvedLoadout);
        GameLog.Verbose($"[SkillLoadout][Server] Applied property selection loadout for player {playerId}.");
        return true;
    }

    [Server]
    public void ApplyDefaultSkillsServer()
    {
        ApplyDefaultSkillSlotsServer();
        ServerSlotsChanged?.Invoke();
    }

    [Server]
    private void ApplyDefaultSkillSlotsServer()
    {
        EnsureSlotCountServer();

        if (_skillDatabase != null && _skillDatabase.TryGetDefaultLoadout(out string[] configuredSkillIds) && configuredSkillIds != null && configuredSkillIds.Length >= SlotCount)
        {
            for (int i = 0; i < SlotCount; i++)
                SlotSkillIds[i] = configuredSkillIds[i] ?? string.Empty;

            LogFinalLoadout("ApplyDefaultSkillsServer(config)");
            return;
        }

        if (_skillDatabase == null
            && ProjectConfigRuntime.TryGetSelectionRuleRepository(out SelectionRuleRepository selectionRules)
            && selectionRules.TryGetDefaultLoadout(ProjectConfigConstants.DefaultRuleSetId, ProjectConfigConstants.DefaultModeTag, out DefaultLoadoutRecord defaultLoadout))
        {
            string[] runtimeConfiguredSkillIds = defaultLoadout.ToSlots();
            if (runtimeConfiguredSkillIds != null && runtimeConfiguredSkillIds.Length >= SlotCount)
            {
                for (int i = 0; i < SlotCount; i++)
                    SlotSkillIds[i] = runtimeConfiguredSkillIds[i] ?? string.Empty;

                LogFinalLoadout("ApplyDefaultSkillsServer(runtime-config)");
                return;
            }
        }

        for (int i = 0; i < SlotCount; i++)
        {
            string defaultSkillId = i < _defaultSkillIds.Length ? _defaultSkillIds[i] : string.Empty;
            SlotSkillIds[i] = defaultSkillId ?? string.Empty;
        }

        LogFinalLoadout("ApplyDefaultSkillsServer");
    }

    [Server]
    private void EnsureSlotCountServer()
    {
        if (SlotSkillIds.Count == SlotCount)
            return;

        SlotSkillIds.Clear();
        for (int i = 0; i < SlotCount; i++)
            SlotSkillIds.Add(string.Empty);
    }

    [Server]
    private void LogFinalLoadout(string context)
    {
        GameLog.Verbose(
            $"[SkillLoadout][Server] {context} final slots: " +
            $"0='{GetSkillId(0)}', 1='{GetSkillId(1)}', 2='{GetSkillId(2)}'");
    }
}
