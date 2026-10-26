using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MFramework.Tests.EditMode
{
    public sealed class MFrameworkCoreTests
    {
        [Test]
        public void InitializeAndShutdownFollowRegistrationOrder()
        {
            List<string> calls = new List<string>();
            TestModule<FirstModuleTag> first = new TestModule<FirstModuleTag>("first", calls);
            TestModule<SecondModuleTag> second = new TestModule<SecondModuleTag>("second", calls);
            MFrameworkApp app = new MFrameworkApp()
                .Register(first)
                .Register(second);

            app.Initialize();
            app.Tick(new MFrameTime(0.1f, 0.2f));
            app.Shutdown();

            CollectionAssert.AreEqual(
                new[] { "first.init", "second.init", "first.tick", "second.tick", "second.stop", "first.stop" },
                calls);
            Assert.AreEqual(MFrameworkAppState.Shutdown, app.State);
        }

        [Test]
        public void SameInstanceRegisteredUnderTwoKeysInitializesOnce()
        {
            List<string> calls = new List<string>();
            TestModule<FirstModuleTag> module = new TestModule<FirstModuleTag>("module", calls);
            MFrameworkApp app = new MFrameworkApp()
                .Register<IMFrameworkModule>(module)
                .Register<TestModule<FirstModuleTag>>(module);

            app.Initialize();
            app.Shutdown();

            CollectionAssert.AreEqual(new[] { "module.init", "module.stop" }, calls);
        }

        [Test]
        public void InitializationFailureRollsBackEarlierModules()
        {
            List<string> calls = new List<string>();
            TestModule<FirstModuleTag> first = new TestModule<FirstModuleTag>("first", calls);
            TestModule<SecondModuleTag> failing = new TestModule<SecondModuleTag>("failing", calls, true);
            MFrameworkApp app = new MFrameworkApp()
                .Register(first)
                .Register(failing);

            MFrameworkInitializationException exception = Assert.Throws<MFrameworkInitializationException>(
                () => app.Initialize());

            Assert.AreEqual(typeof(TestModule<SecondModuleTag>), exception.ModuleType);
            Assert.AreEqual(MFrameworkAppState.Faulted, app.State);
            CollectionAssert.AreEqual(
                new[] { "first.init", "failing.init", "failing.stop", "first.stop" },
                calls);
        }

        [Test]
        public void ShutdownBeforeInitializeStillReleasesRegisteredModules()
        {
            List<string> calls = new List<string>();
            TestModule<FirstModuleTag> first = new TestModule<FirstModuleTag>("first", calls);
            TestModule<SecondModuleTag> second = new TestModule<SecondModuleTag>("second", calls);
            MFrameworkApp app = new MFrameworkApp()
                .Register(first)
                .Register(second);

            app.Shutdown();

            CollectionAssert.AreEqual(new[] { "second.stop", "first.stop" }, calls);
            Assert.AreEqual(MFrameworkAppState.Shutdown, app.State);
        }

        private sealed class FirstModuleTag
        {
        }

        private sealed class SecondModuleTag
        {
        }

        private sealed class TestModule<TTag> : IMFrameworkModule, IMFrameworkTickable
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly bool _fail;

            public TestModule(string name, List<string> calls, bool fail = false)
            {
                _name = name;
                _calls = calls;
                _fail = fail;
            }

            public void Initialize(MFrameworkApp app)
            {
                _calls.Add(_name + ".init");
                if (_fail)
                {
                    throw new InvalidOperationException("expected");
                }
            }

            public void Tick(MFrameTime time)
            {
                _calls.Add(_name + ".tick");
            }

            public void Shutdown()
            {
                _calls.Add(_name + ".stop");
            }
        }
    }
}
