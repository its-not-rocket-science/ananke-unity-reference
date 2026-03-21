using UnityEngine;

namespace Ananke
{
    [RequireComponent(typeof(Animator))]
    public class AnimationDriver : MonoBehaviour
    {
        [Header("Animator Parameters")]
        public string primaryStateParameter = "PrimaryState";
        public string speedParameter = "Speed";
        public string injuryBlendParameter = "InjuryBlend";
        public string shockBlendParameter = "ShockBlend";
        public string fearBlendParameter = "FearBlend";
        public string guardWeightParameter = "GuardWeight";
        public string attackWeightParameter = "AttackWeight";
        public string isProneParameter = "IsProne";
        public string isUnconsciousParameter = "IsUnconscious";
        public string isDeadParameter = "IsDead";

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void ApplyHints(AnankeAnimationHints hints, AnankeCondition condition = null)
        {
            if (_animator == null || hints == null)
            {
                return;
            }

            _animator.SetInteger(primaryStateParameter, hints.PrimaryStateCode);
            _animator.SetFloat(speedParameter, hints.LocomotionMagnitude);
            _animator.SetFloat(guardWeightParameter, AnankeAnimationHints.QToFloat(hints.guardingQ));
            _animator.SetFloat(attackWeightParameter, AnankeAnimationHints.QToFloat(hints.attackingQ));
            _animator.SetFloat(shockBlendParameter, AnankeAnimationHints.QToFloat(hints.shockQ));
            _animator.SetFloat(fearBlendParameter, AnankeAnimationHints.QToFloat(hints.fearQ));
            _animator.SetBool(isProneParameter, hints.prone);
            _animator.SetBool(isUnconsciousParameter, hints.unconscious);
            _animator.SetBool(isDeadParameter, hints.dead);
            _animator.SetFloat(injuryBlendParameter, condition?.Shock ?? AnankeAnimationHints.QToFloat(hints.shockQ));
        }
    }
}
