using System;
using System.Collections.Generic;
using FishNet.Object;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ComboSkillInput : NetworkBehaviour
{
    private const float MinimumStepWindowSeconds = 0.01f;

    public readonly struct ComboProgressEvent
    {
        public ComboProgressEvent(int slotIndex, int matchedStepCount, bool isValidPrefix)
        {
            this.slotIndex = slotIndex;
            this.matchedStepCount = matchedStepCount;
            this.isValidPrefix = isValidPrefix;
        }

        public readonly int slotIndex;
        public readonly int matchedStepCount;
        public readonly bool isValidPrefix;
    }

    public enum Token { W, Up }

    [Serializable]
    public class ComboBinding
    {
        public string name = "Skill";
        public int slotIndex = 0;
        public Token[] sequence;
    }

    [Header("Input")]
    [Tooltip("Use InputSystem_Actions -> Player -> HandPush (W / UpArrow).")]
    [SerializeField] private bool useGeneratedInputActions = true;

    [Header("Combo Window")]
    [Tooltip("Max allowed time between consecutive inputs. If exceeded, buffer clears.")]
    [SerializeField] private float stepWindowSeconds = 0.35f;

    [Tooltip("Max number of inputs kept in buffer.")]
    [SerializeField] private int maxBuffer = 6;

    [Header("Bindings")]
    [SerializeField] private List<ComboBinding> bindings = new();

    public event Action<int, string> OnSkillSlotTriggered;
    public event Action<ComboProgressEvent> OnComboProgress;

    private InputSystem_Actions _actions;
    private InputAction _handPushAction;

    private readonly List<Token> _buffer = new();
    private float _lastInputTime = -999f;

    [SerializeField] private bool debugHud = true;

    private void Awake()
    {
        ApplyConfiguredGlobalRules();

        if (useGeneratedInputActions)
        {
            _actions = new InputSystem_Actions();
            _handPushAction = _actions.Player.HandPush;
        }

        if (bindings.Count == 0)
        {
            bindings.Add(new ComboBinding { name = "Skill1", slotIndex = 0, sequence = new[] { Token.W, Token.Up, Token.W } });
            bindings.Add(new ComboBinding { name = "Skill2", slotIndex = 1, sequence = new[] { Token.Up, Token.W, Token.Up } });
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            return;

        _actions?.Enable();

        if (_handPushAction != null)
            _handPushAction.performed += OnHandPushPerformed;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (_handPushAction != null)
            _handPushAction.performed -= OnHandPushPerformed;

        _actions?.Disable();
    }

    private void OnDisable()
    {
        if (_handPushAction != null)
            _handPushAction.performed -= OnHandPushPerformed;

        _actions?.Disable();
    }

    private void OnHandPushPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || IsRaceGameplayBlocked())
            return;

        if (ctx.control is not KeyControl key)
            return;

        Token? token = key.keyCode switch
        {
            Key.W => Token.W,
            Key.UpArrow => Token.Up,
            _ => null
        };

        if (token == null)
            return;

        PushToken(token.Value);
    }

    private bool IsRaceGameplayBlocked()
    {
        return !ResultAreaInteractionGate.ShouldAllowSkillInput(gameObject);
    }

    private void ApplyConfiguredGlobalRules()
    {
        if (!ProjectConfigRuntime.TryGetGlobalRuleRepository(out GlobalRuleRepository repository))
            return;

        if (repository.TryGetFloat(ProjectConfigConstants.GlobalRuleComboInputWindowSeconds, out float configuredStepWindow))
            stepWindowSeconds = Mathf.Max(MinimumStepWindowSeconds, configuredStepWindow);
    }

    private void PushToken(Token token)
    {
        float now = Time.time;

        if (now - _lastInputTime > stepWindowSeconds)
        {
            Debug.Log($"[Combo] window expired ({now - _lastInputTime:0.00}s), clearing buffer");
            _buffer.Clear();
        }

        _lastInputTime = now;
        _buffer.Add(token);
        if (_buffer.Count > maxBuffer)
            _buffer.RemoveAt(0);

        RaiseComboProgressEvents();

        Debug.Log($"[Combo] +{token} | buffer = {string.Join(",", _buffer)}");

        ComboBinding matched = FindExactMatchOnSuffix(_buffer);
        if (matched != null)
        {
            OnSkillSlotTriggered?.Invoke(matched.slotIndex, matched.name);
            _buffer.Clear();
            RaiseComboProgressEvents();
            return;
        }

        if (!CouldBePrefixOfAnyCombo(_buffer))
        {
            Debug.Log($"[Combo] dead-end buffer, clearing: {string.Join(",", _buffer)}");
            _buffer.Clear();
            RaiseComboProgressEvents();
        }
    }

    public bool TryGetSequenceForSlot(int slotIndex, out Token[] sequence)
    {
        sequence = null;
        for (int i = 0; i < bindings.Count; i++)
        {
            ComboBinding binding = bindings[i];
            if (binding == null || binding.slotIndex != slotIndex || binding.sequence == null || binding.sequence.Length == 0)
                continue;

            sequence = (Token[])binding.sequence.Clone();
            return true;
        }

        return false;
    }

    private ComboBinding FindExactMatchOnSuffix(List<Token> buffer)
    {
        foreach (ComboBinding binding in bindings)
        {
            if (binding.sequence == null || binding.sequence.Length == 0)
                continue;

            if (EndsWith(buffer, binding.sequence))
                return binding;
        }

        return null;
    }

    private bool CouldBePrefixOfAnyCombo(List<Token> buffer)
    {
        foreach (ComboBinding binding in bindings)
        {
            if (binding.sequence == null || binding.sequence.Length == 0)
                continue;

            if (IsPrefixMatch(buffer, binding.sequence))
                return true;
        }

        return false;
    }

    private static bool EndsWith(List<Token> buffer, Token[] sequence)
    {
        if (buffer.Count < sequence.Length)
            return false;

        int start = buffer.Count - sequence.Length;
        for (int i = 0; i < sequence.Length; i++)
        {
            if (buffer[start + i] != sequence[i])
                return false;
        }

        return true;
    }

    private static bool IsPrefixMatch(List<Token> buffer, Token[] sequence)
    {
        if (buffer.Count > sequence.Length)
            return false;

        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] != sequence[i])
                return false;
        }

        return true;
    }

    private void RaiseComboProgressEvents()
    {
        if (OnComboProgress == null)
            return;

        HashSet<int> seenSlots = new();
        for (int i = 0; i < bindings.Count; i++)
        {
            ComboBinding binding = bindings[i];
            if (binding == null)
                continue;

            int slot = binding.slotIndex;
            if (seenSlots.Contains(slot))
                continue;

            seenSlots.Add(slot);

            int bestMatchedCount = 0;
            bool hasValidPrefix = false;

            for (int j = 0; j < bindings.Count; j++)
            {
                ComboBinding candidate = bindings[j];
                if (candidate == null || candidate.slotIndex != slot || candidate.sequence == null || candidate.sequence.Length == 0)
                    continue;

                if (!IsPrefixMatch(_buffer, candidate.sequence))
                    continue;

                hasValidPrefix = _buffer.Count > 0;
                if (_buffer.Count > bestMatchedCount)
                    bestMatchedCount = _buffer.Count;
            }

            OnComboProgress.Invoke(new ComboProgressEvent(slot, bestMatchedCount, hasValidPrefix));
        }
    }

    private void OnGUI()
    {
        if (!debugHud || !IsOwner)
            return;

        GUI.Label(new Rect(10, 10, 800, 30), $"Combo Buffer: {string.Join(",", _buffer)}");
        GUI.Label(new Rect(10, 30, 800, 30), $"Last Input dt: {(Time.time - _lastInputTime):0.00}s / Window {stepWindowSeconds:0.00}s");
    }
}
