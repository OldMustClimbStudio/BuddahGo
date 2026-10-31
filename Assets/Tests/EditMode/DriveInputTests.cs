using System.Collections.Generic;
using System.IO;
using NewBuddah.PredictionV2.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BuddahGo.Tests
{
    // W held pushes forward, released stops pushing (user decision 2026-10-30): the owner reads W from the Movement
    // action, and BuddahDriveInput resolves it, or an ISteeringOverride's drive flag (AI, dummy), into the replicate
    // throttle. Forward is no longer automatic.
    public class DriveInputTests
    {
        private const string ActionsAssetPath = "Assets/Scripts/InputSystem/InputSystem_Actions.inputactions";
        // Same scoped Input System feature flag as HandPushInputTests.
        private const string RunPlayerUpdatesInEditMode = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";

        private Keyboard _keyboard;
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
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
        }

        [Test]
        public void WIsBoundToMovementInTheAssetAndInTheGeneratedWrapper()
        {
            _actions = new InputSystem_Actions();
            Assert.That(Paths(_actions.Player.Movement), Is.EquivalentTo(new[] { "<Keyboard>/a", "<Keyboard>/d", "<Keyboard>/w", "<Keyboard>/space" }),
                "the wrapper JSON binds W to Movement");

            InputActionAsset asset = InputActionAsset.FromJson(File.ReadAllText(ActionsAssetPath));
            try
            {
                Assert.That(Paths(asset.FindAction("Player/Movement", throwIfNotFound: true)),
                    Is.EquivalentTo(Paths(_actions.Player.Movement)), "the .inputactions file and the wrapper JSON must match");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void WHeldIsFullThrottleAndReleaseIsNone()
        {
            InputSystem.settings.SetInternalFeatureFlag(RunPlayerUpdatesInEditMode, true);
            _playerUpdatesEnabled = true;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _actions = new InputSystem_Actions();
            _actions.Player.Movement.Enable();
            var source = new PlayerBuddahInputSource(_actions.Player.Movement);

            Change(new KeyboardState());
            Assert.That(source.GetThrottle(), Is.EqualTo(0f), "no key: no push (forward is no longer automatic)");

            Change(new KeyboardState(Key.W));
            Assert.That(source.GetThrottle(), Is.EqualTo(1f), "W held pushes");
            Assert.That(source.GetSteering(), Is.EqualTo(0f), "W does not steer");

            Change(new KeyboardState(Key.W, Key.A));
            Assert.That(source.GetThrottle(), Is.EqualTo(1f), "steering while pushing keeps the push");
            Assert.That(source.GetSteering(), Is.EqualTo(-1f));

            Change(new KeyboardState(Key.D));
            Assert.That(source.GetThrottle(), Is.EqualTo(0f), "A / D alone never push");

            Change(new KeyboardState(Key.UpArrow, Key.Space));
            Assert.That(source.GetThrottle(), Is.EqualTo(0f), "↑ and space are not drive keys");

            Change(new KeyboardState());
            Assert.That(source.GetThrottle(), Is.EqualTo(0f), "releasing W stops the push");
        }

        [Test]
        public void ResolvedThrottleFollowsTheGateThenTheOverrideThenW()
        {
            Assert.That(BuddahDriveInput.ResolveThrottle(true, 1f, false, false), Is.EqualTo(1f), "owner holds W");
            Assert.That(BuddahDriveInput.ResolveThrottle(true, 0f, false, false), Is.EqualTo(0f), "owner released W");
            Assert.That(BuddahDriveInput.ResolveThrottle(false, 1f, false, false), Is.EqualTo(0f), "closed movement gate (countdown) ignores W");
            Assert.That(BuddahDriveInput.ResolveThrottle(false, 0f, true, true), Is.EqualTo(0f), "closed movement gate stops the AI too");
            Assert.That(BuddahDriveInput.ResolveThrottle(true, 0f, true, true), Is.EqualTo(1f), "AI drive on, no key needed");
            Assert.That(BuddahDriveInput.ResolveThrottle(true, 1f, true, false), Is.EqualTo(0f), "a parking override (dummy, harness) beats a held W");
        }

        private static List<string> Paths(InputAction action)
        {
            var paths = new List<string>();
            foreach (InputBinding binding in action.bindings) paths.Add(binding.path);
            return paths;
        }

        // One player (Dynamic) input update that processes this keyboard state.
        private void Change(KeyboardState state)
        {
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }
    }
}
