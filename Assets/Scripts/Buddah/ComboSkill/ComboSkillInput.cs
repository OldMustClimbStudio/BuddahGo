using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ComboSkillInput : NetworkBehaviour
{
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

    private InputSystem_Actions _actions;
    private InputAction _handPushAction;

    private readonly List<Token> _buffer = new();
    private float _lastInputTime = -999f;

    [SerializeField] private bool debugHud = true;

    private void Awake()
    {
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
        if (!IsOwner)
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

        Debug.Log($"[Combo] +{token} | buffer = {string.Join(",", _buffer)}");

        ComboBinding matched = FindExactMatchOnSuffix(_buffer);
        if (matched != null)
        {
            OnSkillSlotTriggered?.Invoke(matched.slotIndex, matched.name);
            _buffer.Clear();
            return;
        }

        if (!CouldBePrefixOfAnyCombo(_buffer))
        {
            Debug.Log($"[Combo] dead-end buffer, clearing: {string.Join(",", _buffer)}");
            _buffer.Clear();
        }
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

    private void OnGUI()
    {
        if (!debugHud || !IsOwner)
            return;

        GUI.Label(new Rect(10, 10, 800, 30), $"Combo Buffer: {string.Join(",", _buffer)}");
        GUI.Label(new Rect(10, 30, 800, 30), $"Last Input dt: {(Time.time - _lastInputTime):0.00}s / Window {stepWindowSeconds:0.00}s");
    }
}
