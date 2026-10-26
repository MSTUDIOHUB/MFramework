using System;

namespace MFramework
{
    /// <summary>
    /// 创建只能运行一次的动作节点，并将它们组合成易于阅读的游戏流程。
    /// </summary>
    public static class MAction
    {
        /// <summary>创建一个执行一次回调的动作。</summary>
        /// <param name="callback">节点启动时要执行的回调。</param>
        /// <returns>新的单次使用动作节点。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
        public static IMActionNode Callback(Action callback)
        {
            return new CallbackAction(callback);
        }

        /// <summary>创建一个等待指定时长的动作。</summary>
        /// <param name="seconds">等待时长，单位为秒，不能为负数。</param>
        /// <returns>新的单次使用动作节点。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> 为负数。</exception>
        public static IMActionNode Delay(float seconds)
        {
            return new DelayAction(seconds);
        }

        /// <summary>创建一个在条件成立时完成的动作。</summary>
        /// <param name="condition">节点启动时及每次 Tick 时要检查的条件。</param>
        /// <returns>新的单次使用动作节点。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="condition"/> 为 null。</exception>
        public static IMActionNode WaitUntil(Func<bool> condition)
        {
            return new WaitUntilAction(condition);
        }

        /// <summary>创建一个按顺序运行子节点的动作。</summary>
        /// <param name="children">按照执行顺序排列的单次使用子节点。</param>
        /// <returns>新的顺序动作节点。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="children"/> 或其中的某个元素为 null。</exception>
        public static IMActionNode Sequence(params IMActionNode[] children)
        {
            return new SequenceAction(children);
        }

        /// <summary>创建一个同时运行所有子节点的动作。</summary>
        /// <param name="children">要运行的单次使用子节点。</param>
        /// <returns>新的并行动作节点；所有子节点完成后，该节点才会完成。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="children"/> 或其中的某个元素为 null。</exception>
        public static IMActionNode Parallel(params IMActionNode[] children)
        {
            return new ParallelAction(children);
        }

        /// <summary>创建一个按指定次数生成并运行全新子节点的动作。</summary>
        /// <param name="repeatCount">重复次数，不能为负数。</param>
        /// <param name="actionFactory">动作工厂；每次重复时都必须返回一个全新且非 null 的节点。</param>
        /// <returns>新的重复动作节点。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="repeatCount"/> 为负数。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="actionFactory"/> 为 null。</exception>
        public static IMActionNode Repeat(int repeatCount, Func<IMActionNode> actionFactory)
        {
            return new RepeatAction(repeatCount, actionFactory);
        }

        /// <summary>创建一个由调用方提供的生命周期回调驱动的动作。</summary>
        /// <param name="onTick">每次 Tick 时调用；动作需要结束时返回 Completed。</param>
        /// <param name="onStart">可选回调，节点启动时执行一次。</param>
        /// <param name="onCancel">可选回调，节点被取消时执行。</param>
        /// <returns>新的自定义动作节点。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="onTick"/> 为 null。</exception>
        public static IMActionNode Custom(
            Func<float, MActionTickResult> onTick,
            Action onStart = null,
            Action onCancel = null)
        {
            return new CustomAction(onTick, onStart, onCancel);
        }
    }

    internal sealed class CallbackAction : MActionNode
    {
        private Action _callback;

        public CallbackAction(Action callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        protected override void OnStart()
        {
            _callback();
            Complete();
        }

        protected override void OnTick(float deltaTime)
        {
        }

        protected override void OnTerminated()
        {
            _callback = null;
        }
    }

    internal sealed class DelayAction : MActionNode
    {
        private readonly float _duration;
        private float _elapsed;

        public DelayAction(float duration)
        {
            if (duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "延迟等待时长不能为负数。");
            }

            _duration = duration;
        }

        protected override void OnStart()
        {
            if (_duration == 0f)
            {
                Complete();
            }
        }

        protected override void OnTick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed >= _duration)
            {
                Complete();
            }
        }
    }

    internal sealed class WaitUntilAction : MActionNode
    {
        private Func<bool> _condition;

        public WaitUntilAction(Func<bool> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        protected override void OnStart()
        {
            Evaluate();
        }

        protected override void OnTick(float deltaTime)
        {
            Evaluate();
        }

        protected override void OnTerminated()
        {
            _condition = null;
        }

        private void Evaluate()
        {
            if (_condition())
            {
                Complete();
            }
        }
    }

    internal sealed class SequenceAction : MActionNode
    {
        private readonly IMActionNode[] _children;
        private int _currentIndex;

        public SequenceAction(IMActionNode[] children)
        {
            _children = ValidateChildren(children);
        }

        protected override void OnStart()
        {
            StartCurrentAndAdvanceImmediateNodes();
        }

        protected override void OnTick(float deltaTime)
        {
            IMActionNode current = _children[_currentIndex];
            current.Tick(deltaTime);

            if (current.Status == MActionStatus.Cancelled)
            {
                Cancel();
                return;
            }

            StartCurrentAndAdvanceImmediateNodes();
        }

        protected override void OnCancel()
        {
            if (_currentIndex < _children.Length)
            {
                _children[_currentIndex].Cancel();
            }
        }

        private void StartCurrentAndAdvanceImmediateNodes()
        {
            while (_currentIndex < _children.Length)
            {
                IMActionNode current = _children[_currentIndex];
                if (current.Status == MActionStatus.Created)
                {
                    current.Start();
                }

                if (current.Status == MActionStatus.Completed)
                {
                    _currentIndex++;
                    continue;
                }

                if (current.Status == MActionStatus.Cancelled)
                {
                    Cancel();
                }

                return;
            }

            Complete();
        }

        private static IMActionNode[] ValidateChildren(IMActionNode[] children)
        {
            if (children == null)
            {
                throw new ArgumentNullException(nameof(children));
            }

            IMActionNode[] copy = new IMActionNode[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                copy[i] = children[i] ?? throw new ArgumentNullException(nameof(children), "动作子节点不能为 null。");
            }

            return copy;
        }
    }

    internal sealed class ParallelAction : MActionNode
    {
        private readonly IMActionNode[] _children;

        public ParallelAction(IMActionNode[] children)
        {
            if (children == null)
            {
                throw new ArgumentNullException(nameof(children));
            }

            _children = new IMActionNode[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                _children[i] = children[i] ?? throw new ArgumentNullException(nameof(children), "动作子节点不能为 null。");
            }
        }

        protected override void OnStart()
        {
            for (int i = 0; i < _children.Length; i++)
            {
                _children[i].Start();
            }

            CompleteIfEveryChildFinished();
        }

        protected override void OnTick(float deltaTime)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i].Status == MActionStatus.Running)
                {
                    _children[i].Tick(deltaTime);
                }
            }

            CompleteIfEveryChildFinished();
        }

        protected override void OnCancel()
        {
            for (int i = 0; i < _children.Length; i++)
            {
                _children[i].Cancel();
            }
        }

        protected override void OnTerminated()
        {
            if (Status != MActionStatus.Failed)
            {
                return;
            }

            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i].Status == MActionStatus.Running)
                {
                    _children[i].Cancel();
                }
            }
        }

        private void CompleteIfEveryChildFinished()
        {
            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i].Status == MActionStatus.Cancelled)
                {
                    Cancel();
                    return;
                }

                if (_children[i].Status != MActionStatus.Completed)
                {
                    return;
                }
            }

            Complete();
        }
    }

    internal sealed class RepeatAction : MActionNode
    {
        private readonly int _repeatCount;
        private Func<IMActionNode> _actionFactory;
        private int _completedCount;
        private IMActionNode _current;

        public RepeatAction(int repeatCount, Func<IMActionNode> actionFactory)
        {
            if (repeatCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repeatCount), "重复次数不能为负数。");
            }

            _repeatCount = repeatCount;
            _actionFactory = actionFactory ?? throw new ArgumentNullException(nameof(actionFactory));
        }

        protected override void OnStart()
        {
            StartNextAndAdvanceImmediateNodes();
        }

        protected override void OnTick(float deltaTime)
        {
            _current.Tick(deltaTime);
            if (_current.Status == MActionStatus.Cancelled)
            {
                Cancel();
                return;
            }

            StartNextAndAdvanceImmediateNodes();
        }

        protected override void OnCancel()
        {
            _current?.Cancel();
        }

        protected override void OnTerminated()
        {
            _actionFactory = null;
            _current = null;
        }

        private void StartNextAndAdvanceImmediateNodes()
        {
            while (_completedCount < _repeatCount)
            {
                if (_current == null)
                {
                    _current = _actionFactory();
                    if (_current == null)
                    {
                        throw new InvalidOperationException("Repeat 动作工厂返回了 null。");
                    }

                    _current.Start();
                }

                if (_current.Status != MActionStatus.Completed)
                {
                    return;
                }

                _completedCount++;
                _current = null;
            }

            Complete();
        }
    }

    internal sealed class CustomAction : MActionNode
    {
        private Func<float, MActionTickResult> _onTick;
        private Action _onStart;
        private Action _onCancel;

        public CustomAction(
            Func<float, MActionTickResult> onTick,
            Action onStart,
            Action onCancel)
        {
            _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
            _onStart = onStart;
            _onCancel = onCancel;
        }

        protected override void OnStart()
        {
            _onStart?.Invoke();
        }

        protected override void OnTick(float deltaTime)
        {
            MActionTickResult result = _onTick(deltaTime);
            if (result == MActionTickResult.Completed)
            {
                Complete();
            }
            else if (result != MActionTickResult.Running)
            {
                throw new InvalidOperationException("自定义动作返回了未知的 MActionTickResult 状态。");
            }
        }

        protected override void OnCancel()
        {
            _onCancel?.Invoke();
        }

        protected override void OnTerminated()
        {
            _onTick = null;
            _onStart = null;
            _onCancel = null;
        }
    }
}
