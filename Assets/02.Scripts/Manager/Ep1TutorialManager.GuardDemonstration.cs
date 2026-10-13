using System.Collections.Generic;
using UnityEngine;

public sealed partial class Ep1TutorialManager
{
    [SerializeField, TextArea(2, 4)]
    private string _guardExplanationMessage =
        "아래 방향키를 누르고 있으면 가드합니다.\n가드로 요괴의 공격을 막을 수 있습니다.";

    private GuardDemonstrationFreeze _guardDemonstrationFreeze;
    private SpriteRenderer _guardPracticeRenderer;
    private bool IsGuardDemonstrationActive => _guardDemonstrationFreeze != null;

    private void BeginGuardDemonstration()
    {
        // Keep the real guard/hitbox active while suspending the player's state machine.
        if (_combatPlayer.StateMachine.CurrentState != _combatPlayer.GuardState)
            _combatPlayer.StateMachine.ChangeState(_combatPlayer.GuardState);
        _guard.BeginGuard(false);
        Animator animator = _combatPlayer.Animator;
        if (animator != null)
        {
            int pose = Animator.StringToHash("Base Layer.player_guard_maintain");
            if (animator.HasState(0, pose))
            {
                animator.ResetTrigger("guardStart");
                animator.Play(pose, 0, 0f);
                animator.Update(0f);
            }
            _guardPracticeRenderer = animator.GetComponentInChildren<SpriteRenderer>();
        }
        if (_guardPracticeRenderer == null)
            _guardPracticeRenderer = _combatPlayer.GetComponentInChildren<SpriteRenderer>();
        _guardDemonstrationFreeze = new GuardDemonstrationFreeze(_combatPlayer, _combatInput, _guardPracticeEnemy);
        RefreshGuardDemonstration();
    }

    private void RefreshGuardDemonstration(bool showExplanation = false)
    {
        if (_focusOverlay == null) _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        _focusOverlay.Show(null, null, GetWorldCamera(), _guardExplanationMessage, _continueMessage,
            _continueKeySprite, _font, _popupFontSize, _popupFontStyle, _popupTextColor,
            _popupOutlineColor, _popupOutlineThickness, _dimOpacity, _focusBorderColor,
            _focusPadding, _popupTextSize, 24f, new Vector2(240f, 180f), _guardPracticeRenderer, showExplanation);
    }

    private void ReleaseGuardDemonstration()
    {
        if (!IsGuardDemonstrationActive) return;
        _guardDemonstrationFreeze.Restore();
        _guardDemonstrationFreeze = null;
        _guardPracticeRenderer = null;
        _guardPracticeAttackStarted = false;
        if (_focusOverlay != null) _focusOverlay.Hide();
        if (_combatInput != null) _combatInput.ClearInputState();
        if (_combatPlayer != null && _combatPlayer.StateMachine.CurrentState == _combatPlayer.GuardState)
        {
            _combatPlayer.StateMachine.ChangeState(_combatPlayer.Health.IsDead ?
                _combatPlayer.DeadState : _combatPlayer.IdleState);
        }
    }

    // Time keeps running for the demonstration enemy's native AI, animation and hitbox.
    // Save every changed setting so cancellation/death also restores normal play.
    private sealed class GuardDemonstrationFreeze
    {
        private readonly List<Behaviour> _behaviours = new List<Behaviour>();
        private readonly Dictionary<Animator, float> _animators = new Dictionary<Animator, float>();
        private readonly Dictionary<Rigidbody2D, BodySnapshot> _bodies = new Dictionary<Rigidbody2D, BodySnapshot>();

        private readonly struct BodySnapshot
        {
            internal readonly bool Simulated;
            internal readonly RigidbodyConstraints2D Constraints;
            internal readonly Vector2 Velocity;
            internal readonly float AngularVelocity;

            internal BodySnapshot(Rigidbody2D body)
            {
                Simulated = body.simulated;
                Constraints = body.constraints;
                Velocity = body.linearVelocity;
                AngularVelocity = body.angularVelocity;
            }
        }

        internal GuardDemonstrationFreeze(Player player, PlayerInputHandler input, Enemy movingEnemy)
        {
            Suspend(player);
            Suspend(input);
            Suspend(player.Movement);
            foreach (EnemySpawnActivator activator in FindObjectsByType<EnemySpawnActivator>(FindObjectsSortMode.None))
                Suspend(activator);
            foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                if (enemy != movingEnemy) Suspend(enemy);
            foreach (MovingPlatform platform in FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None))
                Suspend(platform);
            foreach (Projectile projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                Suspend(projectile);
            foreach (Animator animator in FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (animator.transform.IsChildOf(movingEnemy.transform)) continue;
                _animators.Add(animator, animator.speed);
                animator.speed = 0f;
            }
            foreach (Rigidbody2D body in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
            {
                if (body.transform.IsChildOf(movingEnemy.transform)) continue;
                _bodies.Add(body, new BodySnapshot(body));
                // Keep colliders simulated: the monster still needs floor contacts and the player's hitbox.
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }

        private void Suspend(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled) return;
            _behaviours.Add(behaviour);
            behaviour.enabled = false;
        }

        internal void Restore()
        {
            foreach (KeyValuePair<Rigidbody2D, BodySnapshot> entry in _bodies)
            {
                if (entry.Key == null) continue;
                entry.Key.simulated = entry.Value.Simulated;
                entry.Key.constraints = entry.Value.Constraints;
                entry.Key.linearVelocity = entry.Value.Velocity;
                entry.Key.angularVelocity = entry.Value.AngularVelocity;
            }
            foreach (KeyValuePair<Animator, float> entry in _animators)
                if (entry.Key != null) entry.Key.speed = entry.Value;
            foreach (Behaviour behaviour in _behaviours)
                if (behaviour != null) behaviour.enabled = true;
        }
    }
}
