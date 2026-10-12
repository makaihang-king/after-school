using System;
using UnityEngine;

namespace Childhood
{
    /// <summary>
    /// 一次真实 PhysX 出手停稳后的结构化结果。该对象只描述结果，不改变正式对局的计分或存档。
    /// </summary>
    [Serializable]
    public sealed class ShotObservation
    {
        public int version = 1;
        public int turn;
        public bool playerControlled;
        public ShotKind shot;
        // shotKind 是面向教学/QA 的语义别名，shot 保持和 PogMatch 的旧字段一致。
        public ShotKind shotKind;
        public float normalizedPower;
        public float normalizedSpin;
        public int collisionCount;
        public int targetCollisionCount;
        public int collisions;
        public int targetCollisions;
        public float maxMovement;
        public float lowestContactNormalY = 1f;
        public float finalTargetUpDot = 1f;
        public Vector3 finalTargetPosition;
        public Quaternion finalTargetRotation = Quaternion.identity;
        public float finalStrikerUpDot = 1f;
        public Vector3 finalStrikerPosition;
        public Quaternion finalStrikerRotation = Quaternion.identity;
        public bool flipped;
        public bool ringOut;
        public bool scored;
        public int scoredTargetCount;

        public bool IsPlayerShot { get { return playerControlled; } }
        public bool AnyTargetFlipped { get { return flipped; } }
        public bool AnyTargetOutOfRing { get { return ringOut; } }
    }

    public enum GuidedPracticeStep
    {
        DragAndCancel = 0,
        SlapFlip = 1,
        SkimRingOut = 2
    }

    [Serializable]
    public sealed class GuidedPracticeProgress
    {
        public GuidedPracticeStep step;
        public bool cancelledOnce;
        public bool stepCompleted;
        public bool allCompleted;
        public bool resultPending;
        public bool awaitingRetry;
        public int attempts;
        public string feedback;
    }

    /// <summary>
    /// 分步教学的判定器。场景负责通过 PogMatch.Begin 摆出练习局，本组件只监听真实
    /// AimCancelled/ShotSettled 事件并给 UI 发出可本地化的状态和重摆请求。
    /// </summary>
    public sealed class GuidedPractice : MonoBehaviour
    {
        [SerializeField] PogMatch match;
        [SerializeField] bool autoAdvance;
        readonly GuidedPracticeProgress progress = new GuidedPracticeProgress();
        public event Action<GuidedPracticeProgress> ProgressChanged;
        public event Action<GuidedPracticeStep> StepCompleted;
        public event Action<GuidedPracticeStep, string> StepFailed;
        public event Action<GuidedPracticeStep> ResetRequested;

        public PogMatch Match { get { return match; } }
        public GuidedPracticeStep Step { get { return progress.step; } }
        public bool IsComplete { get { return progress.allCompleted; } }
        public bool StepComplete { get { return progress.stepCompleted; } }
        public bool HasResult { get { return progress.resultPending; } }
        public bool AutoAdvance { get { return autoAdvance; } set { autoAdvance = value; } }
        public GuidedPracticeProgress Progress { get { return progress; } }

        void OnEnable()
        {
            if (match != null) Subscribe(match);
        }

        void OnDisable()
        {
            if (match != null) Unsubscribe(match);
        }

        public void Attach(PogMatch target)
        {
            if (match == target) return;
            if (match != null) Unsubscribe(match);
            match = target;
            if (isActiveAndEnabled && match != null) Subscribe(match);
        }

        public void Begin(PogMatch target, GuidedPracticeStep firstStep = GuidedPracticeStep.DragAndCancel)
        {
            if (!Enum.IsDefined(typeof(GuidedPracticeStep), firstStep)) throw new ArgumentOutOfRangeException("firstStep");
            Attach(target);
            progress.step = firstStep;
            progress.cancelledOnce = false;
            progress.stepCompleted = false;
            progress.allCompleted = false;
            progress.resultPending = false;
            progress.awaitingRetry = false;
            progress.attempts = 0;
            progress.feedback = Prompt(firstStep);
            RaiseChanged();
        }

        public void SetStep(GuidedPracticeStep step)
        {
            if (!Enum.IsDefined(typeof(GuidedPracticeStep), step)) throw new ArgumentOutOfRangeException("step");
            progress.step = step;
            progress.cancelledOnce = false;
            progress.stepCompleted = false;
            progress.allCompleted = false;
            progress.resultPending = false;
            progress.awaitingRetry = false;
            progress.attempts = 0;
            progress.feedback = Prompt(step);
            RaiseChanged();
        }

        /// <summary>重摆当前步骤，保留已经完成的取消动作和该步骤累计尝试次数。</summary>
        public void RetryStep()
        {
            if (progress.allCompleted) return;
            progress.stepCompleted = false;
            progress.resultPending = false;
            progress.awaitingRetry = false;
            progress.feedback = progress.step == GuidedPracticeStep.DragAndCancel && progress.cancelledOnce
                ? "取消动作已经学会了。再拖一次圆片，松手完成出手。"
                : Prompt(progress.step);
            ResetRequested?.Invoke(progress.step);
            RaiseChanged();
        }

        public bool AdvanceStep()
        {
            if (!progress.stepCompleted || progress.allCompleted) return false;
            if (progress.step == GuidedPracticeStep.SkimRingOut)
            {
                progress.allCompleted = true;
                progress.awaitingRetry = false;
                progress.feedback = "三个动作都练会了。可以回到街区，和小雨约一场不押片的对局。";
                RaiseChanged();
                return true;
            }
            progress.step = (GuidedPracticeStep)((int)progress.step + 1);
            progress.cancelledOnce = false;
            progress.stepCompleted = false;
            progress.resultPending = false;
            progress.awaitingRetry = false;
            progress.attempts = 0;
            progress.feedback = Prompt(progress.step);
            ResetRequested?.Invoke(progress.step);
            RaiseChanged();
            return true;
        }

        void Subscribe(PogMatch target)
        {
            target.AimCancelled += OnAimCancelled;
            target.ShotSettled += OnShotSettled;
        }

        void Unsubscribe(PogMatch target)
        {
            target.AimCancelled -= OnAimCancelled;
            target.ShotSettled -= OnShotSettled;
        }

        void OnAimCancelled()
        {
            if (progress.allCompleted || progress.resultPending || progress.step != GuidedPracticeStep.DragAndCancel) return;
            progress.cancelledOnce = true;
            progress.feedback = "取消成功。再拖一次圆片，松手完成出手。";
            RaiseChanged();
        }

        void OnShotSettled(ShotObservation observation)
        {
            if (observation == null || !observation.playerControlled || progress.allCompleted || progress.resultPending) return;
            progress.attempts++;
            bool success;
            string failure;
            switch (progress.step)
            {
                case GuidedPracticeStep.DragAndCancel:
                    success = progress.cancelledOnce && observation.normalizedPower > .06f;
                    failure = progress.cancelledOnce ? "取消动作已经学会了。这次拖得太短，再试时多向后拉一点。" : "这次出手已停稳。再试时先向后拖，右键取消一次，然后重新拖拽出手。";
                    break;
                case GuidedPracticeStep.SlapFlip:
                    bool mediumPower = observation.normalizedPower >= .25f && observation.normalizedPower <= .78f;
                    success = observation.shotKind == ShotKind.Slap && mediumPower && observation.targetCollisionCount > 0 && observation.flipped;
                    failure = observation.shotKind != ShotKind.Slap ? "这一步用掌拍：选择掌拍后，从圆片一侧用中等力道出手。" :
                        observation.targetCollisionCount == 0 ? "这次没有碰到目标片。再试时把方向对准片的近侧。" :
                        !mediumPower ? (observation.flipped ? "目标片翻面了。这一步再练一次中等力道，把力道保持在提示区域。" :
                            observation.normalizedPower < .25f ? "碰到目标片了，但力道偏轻。再试时多向后拉一点。" : "碰到目标片了，但力道偏重。再试时少向后拉一点。") :
                        "已经碰到目标片，停稳时正面仍朝上。再试时把落点偏向片边，先保持不旋。";
                    break;
                default:
                    success = observation.shotKind == ShotKind.Skim && observation.targetCollisionCount > 0 && observation.ringOut;
                    failure = observation.shotKind != ShotKind.Skim ? "这一步用斜擦：贴地从圈内侧向外送力。" :
                        observation.targetCollisionCount == 0 ? "这次没有碰到目标片。再试时对准片的圈内侧，贴地朝圈外擦。" :
                        "碰到目标片了，但整张还没有越过圈线。再试时朝最近的圈边，多向后拉一点。";
                    break;
            }
            // 先锁住本次结果，再通知场景/UI，防止重复事件和同步重摆丢掉真实结果。
            progress.resultPending = true;
            progress.awaitingRetry = !success;
            if (success)
            {
                progress.stepCompleted = true;
                progress.feedback = Success(progress.step);
                StepCompleted?.Invoke(progress.step);
                if (autoAdvance) AdvanceStep();
            }
            else
            {
                progress.feedback = failure;
                StepFailed?.Invoke(progress.step, failure);
            }
            RaiseChanged();
        }

        static string Prompt(GuidedPracticeStep step)
        {
            switch (step)
            {
                case GuidedPracticeStep.DragAndCancel: return "按住圆片向后拖动，右键取消一次，再拖拽松手出击。";
                case GuidedPracticeStep.SlapFlip: return "选择掌拍，用中等力道碰到目标片的一侧，让它翻面并停稳。";
                default: return "选择斜擦，从粉笔圈内侧向外送力，让目标片完整越过圈线。";
            }
        }

        static string Success(GuidedPracticeStep step)
        {
            switch (step)
            {
                case GuidedPracticeStep.DragAndCancel: return "拖拽、取消和重新出手都完成了。";
                case GuidedPracticeStep.SlapFlip: return "掌拍翻面成功，记住落点比蛮力更重要。";
                default: return "斜擦出圈成功，三个动作都学会了。回到街区，就能和小雨约一场。";
            }
        }

        void RaiseChanged() { ProgressChanged?.Invoke(progress); }
    }
}
