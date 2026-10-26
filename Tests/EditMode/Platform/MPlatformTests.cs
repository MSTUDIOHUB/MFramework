using System;
using NUnit.Framework;
using UnityEngine;

namespace MFramework.Tests.EditMode
{
    internal sealed class MPlatformTests
    {
        [Test]
        public void EditorBridge_InitializesAndRoundTripsStringStorage()
        {
            string key = $"mframework.tests.{Guid.NewGuid():N}";
            var bridge = new MEditorPlatformBridge();
            MPlatformInitializationResult result = default;

            try
            {
                bridge.Initialize(value => result = value);
                bridge.SaveString(key, "stored value");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.ErrorMessage, Is.Empty);
                Assert.That(bridge.IsReady, Is.True);
                Assert.That(bridge.LoadString(key, "fallback"), Is.EqualTo("stored value"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void Service_CoalescesSimultaneousInitializationRequests()
        {
            var bridge = new MDeferredPlatformBridge();
            var service = new MPlatformService(bridge);
            int callbackCount = 0;

            service.Initialize(result =>
            {
                Assert.That(result.Succeeded, Is.True);
                callbackCount++;
            });
            service.Initialize(result =>
            {
                Assert.That(result.Succeeded, Is.True);
                callbackCount++;
            });

            Assert.That(bridge.InitializeCallCount, Is.EqualTo(1));
            Assert.That(callbackCount, Is.Zero);

            bridge.CompleteSuccessfully();

            Assert.That(callbackCount, Is.EqualTo(2));
            Assert.That(service.IsReady, Is.True);

            service.Initialize(result => callbackCount++);
            Assert.That(callbackCount, Is.EqualTo(3));
            Assert.That(bridge.InitializeCallCount, Is.EqualTo(1));
        }

        [Test]
        public void FailureResult_RequiresUsefulErrorMessage()
        {
            Assert.Throws<ArgumentException>(() => MPlatformInitializationResult.Failure(string.Empty));

            MPlatformInitializationResult result = MPlatformInitializationResult.Failure("SDK unavailable");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("SDK unavailable"));
        }

        [Test]
        public void Shutdown_ClearsWaitingCallbacksAndIgnoresLateBridgeResult()
        {
            var bridge = new MDeferredPlatformBridge();
            var service = new MPlatformService(bridge);
            int callbackCount = 0;

            ((IMFrameworkModule)service).Initialize(null);
            service.Initialize(_ => callbackCount++);
            service.Shutdown();

            Assert.DoesNotThrow(() => bridge.CompleteSuccessfully());
            Assert.That(callbackCount, Is.Zero, "关闭时等待中的初始化回调不应在稍后执行。");
            Assert.DoesNotThrow(() => service.Shutdown());
            Assert.DoesNotThrow(() => service.Dispose());
        }

        [Test]
        public void DisposedService_RejectsFurtherPlatformCalls()
        {
            var service = new MPlatformService(new MDeferredPlatformBridge());
            service.Dispose();

            Assert.Throws<ObjectDisposedException>(() => service.Initialize(_ => { }));
            Assert.Throws<ObjectDisposedException>(() => service.VibrateShort());
            Assert.Throws<ObjectDisposedException>(() => service.LoadString("key", "fallback"));
            Assert.Throws<ObjectDisposedException>(() => service.SaveString("key", "value"));
            Assert.Throws<ObjectDisposedException>(() => service.Name.ToString());
        }

        [Test]
        public void Retry_IgnoresLateResultFromEarlierInitializationGeneration()
        {
            var bridge = new MGenerationPlatformBridge();
            var service = new MPlatformService(bridge);
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;

            bridge.ThrowAfterCapturingNextCallback = true;
            service.Initialize(_ => firstCallbackCount++);
            service.Initialize(_ => secondCallbackCount++);

            bridge.Complete(0, MPlatformInitializationResult.Success());
            Assert.That(secondCallbackCount, Is.Zero, "上一轮迟到结果不能完成新的初始化批次。");

            bridge.IsReady = true;
            bridge.Complete(1, MPlatformInitializationResult.Success());

            Assert.That(firstCallbackCount, Is.EqualTo(1));
            Assert.That(secondCallbackCount, Is.EqualTo(1));
        }

        [Test]
        public void DefaultFailureResult_IsNormalizedWithUsefulMessage()
        {
            var bridge = new MDeferredPlatformBridge();
            var service = new MPlatformService(bridge);
            MPlatformInitializationResult received = default;

            service.Initialize(result => received = result);
            bridge.Complete(default);

            Assert.That(received.Succeeded, Is.False);
            Assert.That(received.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void UnreadyOrThrowingBridge_UsesSafeFallbacks()
        {
            var bridge = new MFailingPlatformBridge();
            var service = new MPlatformService(bridge);

            Assert.DoesNotThrow(() => service.VibrateShort());
            Assert.That(service.LoadString("key", "fallback"), Is.EqualTo("fallback"));
            Assert.DoesNotThrow(() => service.SaveString("key", "value"));

            bridge.IsReady = true;
            Assert.DoesNotThrow(() => service.VibrateShort());
            Assert.That(service.LoadString("key", "fallback"), Is.EqualTo("fallback"));
            Assert.DoesNotThrow(() => service.SaveString("key", "value"));
        }
    }

    internal sealed class MDeferredPlatformBridge : IMPlatformBridge
    {
        private Action<MPlatformInitializationResult> _completed;

        public string Name => "Deferred Test Bridge";

        public bool IsReady { get; private set; }

        public int InitializeCallCount { get; private set; }

        public void Initialize(Action<MPlatformInitializationResult> completed)
        {
            InitializeCallCount++;
            _completed = completed;
        }

        public void VibrateShort()
        {
        }

        public string LoadString(string key, string fallback)
        {
            return fallback;
        }

        public void SaveString(string key, string value)
        {
        }

        public void CompleteSuccessfully()
        {
            IsReady = true;
            _completed(MPlatformInitializationResult.Success());
        }

        public void Complete(MPlatformInitializationResult result)
        {
            _completed(result);
        }
    }

    internal sealed class MGenerationPlatformBridge : IMPlatformBridge
    {
        private readonly System.Collections.Generic.List<Action<MPlatformInitializationResult>> _callbacks =
            new System.Collections.Generic.List<Action<MPlatformInitializationResult>>();

        public string Name => "Generation Test Bridge";

        public bool IsReady { get; set; }

        public bool ThrowAfterCapturingNextCallback { get; set; }

        public void Initialize(Action<MPlatformInitializationResult> completed)
        {
            _callbacks.Add(completed);
            if (ThrowAfterCapturingNextCallback)
            {
                ThrowAfterCapturingNextCallback = false;
                throw new InvalidOperationException("expected initialization failure");
            }
        }

        public void VibrateShort()
        {
        }

        public string LoadString(string key, string fallback)
        {
            return fallback;
        }

        public void SaveString(string key, string value)
        {
        }

        public void Complete(int index, MPlatformInitializationResult result)
        {
            _callbacks[index](result);
        }
    }

    internal sealed class MFailingPlatformBridge : IMPlatformBridge
    {
        public string Name => "Failing Test Bridge";

        public bool IsReady { get; set; }

        public void Initialize(Action<MPlatformInitializationResult> completed)
        {
            throw new InvalidOperationException("expected");
        }

        public void VibrateShort()
        {
            throw new InvalidOperationException("expected");
        }

        public string LoadString(string key, string fallback)
        {
            throw new InvalidOperationException("expected");
        }

        public void SaveString(string key, string value)
        {
            throw new InvalidOperationException("expected");
        }
    }
}
