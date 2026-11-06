using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BuddahGo.Tests
{
    // Mouse left = left hand, mouse right = right hand on the one HandPush action (the W/↑ push bindings were deleted
    // on 2026-10-30; W is the drive key, see DriveInputTests).
    // A Button action resolves conflicts between its bound controls, so a press of the second button while the
    // first is held never reaches `performed`; HandPush is PassThrough and BuddahHandControl ignores releases.
    public class HandPushInputTests
    {
        private const string ActionsAssetPath = "Assets/Scripts/InputSystem/InputSystem_Actions.inputactions";
        // Input System feature flag (InputFeatureNames.kRunPlayerUpdatesInEditMode). Actions ignore editor updates,
        // and an edit-mode editor runs nothing else, so the test turns player updates on for its own duration only
        // (feature flags are never persisted to settings).
        private const string RunPlayerUpdatesInEditMode = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";

        private Mouse _mouse;
        private InputSystem_Actions _actions;
        private bool _playerUpdatesEnabled;

        [TearDown]
        public void TearDown()
        {
            if (_playerUpdatesEnabled)
            {
                InputSystem.settings.SetInternalFeatureFlag(RunPlayerUpdatesInEditMode, false);
                _playerUpdatesEnabled = false;
            }
            if (_actions != null)
            {
                _actions.Player.Disable();
                _actions.Menu.Disable();
                // The wrapper's Dispose uses Object.Destroy, which is not allowed in edit mode.
                Object.DestroyImmediate(_actions.asset);
                _actions = null;
            }
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
        }

        [Test]
        public void HandPushIsPassThroughInTheAssetAndInTheGeneratedWrapper()
        {
            _actions = new InputSystem_Actions();
            Assert.That(_actions.Player.HandPush.type, Is.EqualTo(InputActionType.PassThrough));

            InputActionAsset asset = InputActionAsset.FromJson(File.ReadAllText(ActionsAssetPath));
            try
            {
                InputAction handPush = asset.FindAction("Player/HandPush", throwIfNotFound: true);
                Assert.That(handPush.type, Is.EqualTo(InputActionType.PassThrough), "the .inputactions file and the wrapper JSON must match");
                var paths = new List<string>();
                foreach (InputBinding binding in handPush.bindings) paths.Add(binding.path);
                Assert.That(paths, Is.EquivalentTo(new[] { "<Mouse>/leftButton", "<Mouse>/rightButton" }),
                    "mouse buttons are the only push keys; W drives and ↑ is unbound");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void HoldingOneMouseButtonDoesNotSwallowTheOtherHandsPush()
        {
            InputSystem.settings.SetInternalFeatureFlag(RunPlayerUpdatesInEditMode, true);
            _playerUpdatesEnabled = true;
            _mouse = InputSystem.AddDevice<Mouse>();
            _actions = new InputSystem_Actions();
            var presses = new List<string>();
            int callbacks = 0;
            // Same press filter as BuddahHandControl.OnHandPushPerformed.
            _actions.Player.HandPush.performed += ctx =>
            {
                if (ctx.control.device != _mouse) return;
                callbacks++;
                if (ctx.ReadValueAsButton()) presses.Add(ctx.control.name);
            };
            _actions.Player.HandPush.Enable();

            Change(new MouseState().WithButton(MouseButton.Left));
            Change(new MouseState().WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            Change(new MouseState().WithButton(MouseButton.Right));
            Change(new MouseState());
            Assert.That(presses, Is.EqualTo(new[] { "leftButton", "rightButton" }),
                $"right pressed while left is held still pushes; releases never push (callbacks={callbacks}, update={InputState.currentUpdateType})");
            Assert.That(callbacks, Is.EqualTo(4), "a PassThrough action reports each press and each release");

            presses.Clear();
            Change(new MouseState().WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            Change(new MouseState());
            Assert.That(presses, Is.EquivalentTo(new[] { "leftButton", "rightButton" }), "both buttons in one change push both hands");
        }

        // One player (Dynamic) input update that processes this mouse state.
        private void Change(MouseState state)
        {
            InputSystem.QueueStateEvent(_mouse, state);
            InputSystem.Update();
            Assert.That(_mouse.leftButton.isPressed, Is.EqualTo((state.buttons & (1 << (int)MouseButton.Left)) != 0), "mouse state applied");
        }
    }
}
