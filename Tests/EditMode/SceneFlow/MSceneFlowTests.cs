using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MFramework.Tests.EditMode
{
    public sealed class MSceneFlowTests
    {
        private static readonly MFrameTime TestFrame = new MFrameTime(0.1f, 0.1f);

        private MSceneFlow _flow;

        [TearDown]
        public void TearDown()
        {
            _flow?.Dispose();
            _flow = null;
        }

        [Test]
        public void LoadThenUnloadRunsEveryStageInContractOrder()
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend();
            FakeScene previous = new FakeScene("previous");
            _flow = CreateFlow(backend, previous);

            IReadOnlyList<MSceneFlowStage> stages = RunGatedTransition(
                backend,
                MSceneFlowOrder.LoadThenUnload,
                out MSceneFlowRequest request);

            CollectionAssert.AreEqual(
                new[]
                {
                    MSceneFlowStage.BeforeTransition,
                    MSceneFlowStage.BeforeLoad,
                    MSceneFlowStage.Loading,
                    MSceneFlowStage.AfterLoad,
                    MSceneFlowStage.BeforeActivation,
                    MSceneFlowStage.Activating,
                    MSceneFlowStage.AfterActivation,
                    MSceneFlowStage.BeforeSetActiveScene,
                    MSceneFlowStage.SettingActiveScene,
                    MSceneFlowStage.AfterSetActiveScene,
                    MSceneFlowStage.BeforeUnload,
                    MSceneFlowStage.Unloading,
                    MSceneFlowStage.AfterUnload,
                    MSceneFlowStage.AfterTransition,
                    MSceneFlowStage.Finally,
                    MSceneFlowStage.Completed
                },
                stages);
            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Succeeded));
            Assert.That(request.HasCommitted, Is.True);
            Assert.That(_flow.CurrentScene, Is.SameAs(backend.TargetScene));
            Assert.That(previous.IsReleased, Is.True);
        }

        [Test]
        public void UnloadThenLoadRunsEveryStageInContractOrder()
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend();
            FakeScene previous = new FakeScene("previous");
            _flow = CreateFlow(backend, previous);

            IReadOnlyList<MSceneFlowStage> stages = RunGatedTransition(
                backend,
                MSceneFlowOrder.UnloadThenLoad,
                out MSceneFlowRequest request);

            CollectionAssert.AreEqual(
                new[]
                {
                    MSceneFlowStage.BeforeTransition,
                    MSceneFlowStage.BeforeUnload,
                    MSceneFlowStage.Unloading,
                    MSceneFlowStage.AfterUnload,
                    MSceneFlowStage.BeforeLoad,
                    MSceneFlowStage.Loading,
                    MSceneFlowStage.AfterLoad,
                    MSceneFlowStage.BeforeActivation,
                    MSceneFlowStage.Activating,
                    MSceneFlowStage.AfterActivation,
                    MSceneFlowStage.BeforeSetActiveScene,
                    MSceneFlowStage.SettingActiveScene,
                    MSceneFlowStage.AfterSetActiveScene,
                    MSceneFlowStage.AfterTransition,
                    MSceneFlowStage.Finally,
                    MSceneFlowStage.Completed
                },
                stages);
            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Succeeded));
            Assert.That(request.HasCommitted, Is.True);
            Assert.That(_flow.CurrentScene, Is.SameAs(backend.TargetScene));
            Assert.That(previous.IsReleased, Is.True);
        }

        [Test]
        public void ShortcutTransitionByTargetKeyWorksDirectly()
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend();
            _flow = CreateFlow(backend);

            MSceneFlowRequest request = _flow.Transition("target");

            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Succeeded));
            Assert.That(_flow.CurrentScene, Is.SameAs(backend.TargetScene));
        }

        [Test]
        public void CommandsInTheSameStageRunInRegistrationOrder()
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend();
            _flow = CreateFlow(backend);
            List<string> trace = new List<string>();
            MSceneFlowPlan plan = new MSceneFlowPlan("target")
                .Do(MSceneFlowStage.AfterLoad, _ => trace.Add("A"), "A")
                .Do(MSceneFlowStage.AfterLoad, _ => trace.Add("B"), "B")
                .Do(MSceneFlowStage.AfterLoad, _ => trace.Add("C"), "C");

            MSceneFlowRequest request = _flow.Transition(plan);

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, trace);
            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Succeeded));
        }

        [TestCase(MSceneFlowOrder.LoadThenUnload)]
        [TestCase(MSceneFlowOrder.UnloadThenLoad)]
        public void TransitionWithoutPreviousSceneSkipsEveryUnloadStage(MSceneFlowOrder order)
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend();
            _flow = CreateFlow(backend);

            IReadOnlyList<MSceneFlowStage> stages = RunGatedTransition(
                backend,
                order,
                out MSceneFlowRequest request);

            CollectionAssert.AreEqual(
                new[]
                {
                    MSceneFlowStage.BeforeTransition,
                    MSceneFlowStage.BeforeLoad,
                    MSceneFlowStage.Loading,
                    MSceneFlowStage.AfterLoad,
                    MSceneFlowStage.BeforeActivation,
                    MSceneFlowStage.Activating,
                    MSceneFlowStage.AfterActivation,
                    MSceneFlowStage.BeforeSetActiveScene,
                    MSceneFlowStage.SettingActiveScene,
                    MSceneFlowStage.AfterSetActiveScene,
                    MSceneFlowStage.AfterTransition,
                    MSceneFlowStage.Finally,
                    MSceneFlowStage.Completed
                },
                stages);
            Assert.That(backend.UnloadCallCount, Is.EqualTo(0));
            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Succeeded));
            Assert.That(_flow.CurrentScene, Is.SameAs(backend.TargetScene));
        }

        [Test]
        public void LoadThenUnloadLoadFailureKeepsPreviousSceneUncommitted()
        {
            InvalidOperationException expected = new InvalidOperationException("expected load failure");
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend
            {
                LoadFailure = expected
            };
            FakeScene previous = new FakeScene("previous");
            _flow = CreateFlow(backend, previous);
            int finallyCount = 0;
            MSceneFlowPlan plan = new MSceneFlowPlan("target")
            {
                Order = MSceneFlowOrder.LoadThenUnload
            };
            plan.Do(MSceneFlowStage.Finally, _ => finallyCount++);

            MSceneFlowRequest request = _flow.Transition(plan);

            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Failed));
            Assert.That(request.FailedStage, Is.EqualTo(MSceneFlowStage.Loading));
            Assert.That(request.Failure, Is.SameAs(expected));
            Assert.That(request.HasCommitted, Is.False);
            Assert.That(_flow.CurrentScene, Is.SameAs(previous));
            Assert.That(previous.IsReleased, Is.False);
            Assert.That(backend.ActivateCallCount, Is.EqualTo(0));
            Assert.That(backend.UnloadCallCount, Is.EqualTo(0));
            Assert.That(finallyCount, Is.EqualTo(1));
        }

        [Test]
        public void UnloadThenLoadLoadFailureLeavesNoCurrentSceneAfterCommit()
        {
            InvalidOperationException expected = new InvalidOperationException("expected load failure");
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend
            {
                LoadFailure = expected
            };
            FakeScene previous = new FakeScene("previous");
            _flow = CreateFlow(backend, previous);
            int finallyCount = 0;
            MSceneFlowPlan plan = new MSceneFlowPlan("target")
            {
                Order = MSceneFlowOrder.UnloadThenLoad
            };
            plan.Do(MSceneFlowStage.Finally, _ => finallyCount++);

            MSceneFlowRequest request = _flow.Transition(plan);

            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Failed));
            Assert.That(request.FailedStage, Is.EqualTo(MSceneFlowStage.Loading));
            Assert.That(request.Failure, Is.SameAs(expected));
            Assert.That(request.HasCommitted, Is.True);
            Assert.That(_flow.CurrentScene, Is.Null);
            Assert.That(previous.IsReleased, Is.True);
            Assert.That(backend.UnloadCallCount, Is.EqualTo(1));
            Assert.That(finallyCount, Is.EqualTo(1));
        }

        [Test]
        public void StartingAnotherTransitionWhileBusyIsRejectedWithoutReplacingTheActiveRequest()
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend
            {
                CompleteLoadImmediately = false
            };
            _flow = CreateFlow(backend);
            MSceneFlowRequest active = _flow.Transition(new MSceneFlowPlan("first"));

            Assert.Throws<InvalidOperationException>(
                () => _flow.Transition(new MSceneFlowPlan("second")));

            Assert.That(_flow.IsBusy, Is.True);
            Assert.That(_flow.ActiveRequest, Is.SameAs(active));
            Assert.That(active.State, Is.EqualTo(MSceneFlowState.Running));
            backend.LastLoadOperation.Complete();
            _flow.Tick(TestFrame);
            Assert.That(active.State, Is.EqualTo(MSceneFlowState.Succeeded));
            Assert.That(_flow.IsBusy, Is.False);
        }

        [Test]
        public void CancellationWaitsForLoadAndTargetCleanupOperationsBeforeCompleting()
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend
            {
                CompleteLoadImmediately = false,
                CompleteUnloadImmediately = false
            };
            FakeScene previous = new FakeScene("previous");
            _flow = CreateFlow(backend, previous);
            int finallyCount = 0;
            MSceneFlowPlan plan = new MSceneFlowPlan("target")
                .Do(MSceneFlowStage.Finally, _ => finallyCount++);
            MSceneFlowRequest request = _flow.Transition(plan);

            Assert.That(request.Cancel(), Is.True);

            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Cancelling));
            Assert.That(request.IsDone, Is.False);
            Assert.That(_flow.IsBusy, Is.True);
            Assert.That(backend.LastLoadOperation.DisposeCount, Is.EqualTo(0));
            Assert.That(finallyCount, Is.EqualTo(0));

            backend.LastLoadOperation.Complete();
            _flow.Tick(TestFrame);

            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Cancelling));
            Assert.That(request.Stage, Is.EqualTo(MSceneFlowStage.CleaningTarget));
            Assert.That(request.IsDone, Is.False);
            Assert.That(_flow.IsBusy, Is.True);
            Assert.That(backend.UnloadCallCount, Is.EqualTo(1));
            Assert.That(backend.LastUnloadOperation.IsDone, Is.False);
            Assert.That(finallyCount, Is.EqualTo(0));

            backend.LastUnloadOperation.Complete();
            _flow.Tick(TestFrame);

            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Cancelled));
            Assert.That(request.Stage, Is.EqualTo(MSceneFlowStage.Cancelled));
            Assert.That(request.IsDone, Is.True);
            Assert.That(_flow.IsBusy, Is.False);
            Assert.That(_flow.CurrentScene, Is.SameAs(previous));
            Assert.That(previous.IsReleased, Is.False);
            Assert.That(backend.TargetScene.IsReleased, Is.True);
            Assert.That(backend.LastLoadOperation.DisposeCount, Is.EqualTo(1));
            Assert.That(backend.LastUnloadOperation.DisposeCount, Is.EqualTo(1));
            Assert.That(finallyCount, Is.EqualTo(1));
        }

        [Test]
        public void FinallyRunsAfterSuccessfulAndFailedTransitions()
        {
            FakeSceneFlowBackend backend = new FakeSceneFlowBackend();
            _flow = CreateFlow(backend);
            List<string> trace = new List<string>();
            MSceneFlowPlan plan = new MSceneFlowPlan("target")
                .Do(MSceneFlowStage.AfterTransition, _ => trace.Add("after"))
                .Do(MSceneFlowStage.Finally, _ => trace.Add("finally"));

            MSceneFlowRequest request = _flow.Transition(plan);

            CollectionAssert.AreEqual(new[] { "after", "finally" }, trace);
            Assert.That(request.State, Is.EqualTo(MSceneFlowState.Succeeded));
        }

        private static MSceneFlow CreateFlow(IMSceneFlowBackend backend, IMSceneFlowScene initialScene = null)
        {
            MSceneFlow flow = new MSceneFlow(backend);
            if (initialScene != null)
            {
                flow.AdoptCurrentScene(initialScene);
            }

            return flow;
        }

        private IReadOnlyList<MSceneFlowStage> RunGatedTransition(
            FakeSceneFlowBackend backend,
            MSceneFlowOrder order,
            out MSceneFlowRequest request)
        {
            backend.CompleteLoadImmediately = false;
            backend.CompleteActivateImmediately = false;
            backend.CompleteUnloadImmediately = false;

            List<MSceneFlowStage> stages = new List<MSceneFlowStage>();
            MSceneFlowPlan plan = new MSceneFlowPlan("target")
            {
                Order = order
            };
            plan.Do(MSceneFlowStage.BeforeTransition, context =>
            {
                // Transition 会同步推进阶段；在首个阶段命令中订阅，才能观察后续所有阶段。
                stages.Add(context.Request.Stage);
                context.Request.StageChanged += current => stages.Add(current.Stage);
            });

            request = _flow.Transition(plan);

            for (int step = 0; step < 8 && !request.IsDone; step++)
            {
                if (backend.LastLoadOperation != null && !backend.LastLoadOperation.IsDone)
                {
                    backend.LastLoadOperation.Complete();
                }
                else if (backend.LastActivateOperation != null && !backend.LastActivateOperation.IsDone)
                {
                    backend.LastActivateOperation.Complete();
                }
                else if (backend.LastUnloadOperation != null && !backend.LastUnloadOperation.IsDone)
                {
                    backend.LastUnloadOperation.Complete();
                }

                _flow.Tick(TestFrame);
            }

            return stages;
        }

        private sealed class FakeScene : IMSceneFlowScene
        {
            public FakeScene(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public UnityEngine.SceneManagement.Scene Scene => default;
            public bool IsReleased { get; private set; }

            public void Dispose()
            {
                IsReleased = true;
            }
        }

        private sealed class FakeSceneFlowBackend : IMSceneFlowBackend
        {
            public FakeScene TargetScene { get; } = new FakeScene("target");
            public bool CompleteLoadImmediately { get; set; } = true;
            public bool CompleteActivateImmediately { get; set; } = true;
            public bool CompleteUnloadImmediately { get; set; } = true;
            public Exception LoadFailure { get; set; }
            public Exception ActivationFailure { get; set; }
            public Exception UnloadFailure { get; set; }
            public int LoadCallCount { get; private set; }
            public int ActivateCallCount { get; private set; }
            public int UnloadCallCount { get; private set; }
            public FakeLoadOperation LastLoadOperation { get; private set; }
            public FakeOperation LastActivateOperation { get; private set; }
            public FakeOperation LastUnloadOperation { get; private set; }

            public IMSceneFlowLoadOperation LoadAsync(object key, int priority)
            {
                LoadCallCount++;
                LastLoadOperation = new FakeLoadOperation(TargetScene, CompleteLoadImmediately, LoadFailure);
                return LastLoadOperation;
            }

            public IMSceneFlowOperation ActivateAsync(IMSceneFlowScene scene)
            {
                ActivateCallCount++;
                LastActivateOperation = new FakeOperation(CompleteActivateImmediately, ActivationFailure);
                return LastActivateOperation;
            }

            public IMSceneFlowOperation UnloadAsync(IMSceneFlowScene scene)
            {
                UnloadCallCount++;
                LastUnloadOperation = new FakeOperation(
                    CompleteUnloadImmediately,
                    UnloadFailure,
                    () => scene.Dispose());
                return LastUnloadOperation;
            }

            public void SetActiveScene(IMSceneFlowScene scene)
            {
            }
        }

        private class FakeOperation : IMSceneFlowOperation
        {
            private readonly Action _onComplete;

            public FakeOperation(bool isDone, Exception failure, Action onComplete = null)
            {
                IsDone = isDone;
                Failure = failure;
                _onComplete = onComplete;
                if (isDone && failure == null)
                {
                    _onComplete?.Invoke();
                }
            }

            public bool IsDone { get; private set; }
            public float Progress => IsDone ? 1f : 0.5f;
            public Exception Failure { get; private set; }
            public int DisposeCount { get; private set; }

            public void Complete(Exception failure = null)
            {
                Failure = failure;
                IsDone = true;
                if (failure == null)
                {
                    _onComplete?.Invoke();
                }
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class FakeLoadOperation : FakeOperation, IMSceneFlowLoadOperation
        {
            private IMSceneFlowScene _scene;

            public FakeLoadOperation(IMSceneFlowScene scene, bool isDone, Exception failure)
                : base(isDone, failure)
            {
                _scene = scene;
            }

            public IMSceneFlowScene TakeScene()
            {
                IMSceneFlowScene scene = _scene;
                _scene = null;
                return scene;
            }
        }
    }
}
