using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MFramework.Tests.EditMode
{
    public sealed class MConfigTests
    {
        private readonly List<MConfigAsset> _createdAssets = new List<MConfigAsset>();

        [TearDown]
        public void TearDown()
        {
            foreach (MConfigAsset config in _createdAssets)
            {
                if (config != null)
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            }

            _createdAssets.Clear();
        }

        [Test]
        public void RegisterAndGetUseTypeAndStableId()
        {
            MConfigTestAsset config = CreateConfig("soldier", 10);
            MConfig configs = new MConfig();

            configs.Register(config);

            Assert.AreSame(config, configs.Get<MConfigTestAsset>("soldier"));
            Assert.AreEqual(1, configs.GetAll<MConfigTestAsset>().Count);
        }

        [Test]
        public void DuplicateIdsAreRejectedWithinTheSameType()
        {
            MConfigTestAsset config = CreateConfig("soldier", 10);
            MConfigTestAsset duplicate = CreateConfig("soldier", 20);
            MConfig configs = new MConfig();
            configs.Register(config);

            Assert.Throws<MConfigRegistrationException>(() => configs.Register(duplicate));
        }

        [Test]
        public void ValidateAllCollectsDomainErrors()
        {
            MConfigTestAsset config = CreateConfig("soldier", -1);
            MConfig configs = new MConfig();
            configs.Register(config);

            var errors = configs.ValidateAll();

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("金币必须大于或等于零。", errors[0].Message);
            Assert.AreEqual(typeof(MConfigTestAsset), errors[0].ConfigType);
            Assert.AreEqual("soldier", errors[0].ConfigId);
            Assert.IsNull(errors[0].FieldPath);
            Assert.AreEqual("MConfigTestAsset[soldier]: 金币必须大于或等于零。", errors[0].ToString());
        }

        [Test]
        public void ReferencesResolveAfterAllTypesHaveBeenRegistered()
        {
            MConfigReferenceTestAsset source = CreateReference("panel", "title", "help");
            MConfig configs = new MConfig();
            configs.Register(source);
            configs.RegisterRange(new[] { CreateConfig("help", 0), CreateConfig("title", 0) });

            Assert.IsEmpty(configs.ValidateAll());
        }

        [Test]
        public void MissingReferencesReportBothRequiredAndFilledOptionalFields()
        {
            MConfigReferenceTestAsset source = CreateReference("panel", "missing-title", "missing-help");
            MConfig configs = new MConfig();
            configs.Register(source);

            var errors = configs.ValidateAll();

            Assert.AreEqual(2, errors.Count);
            CollectionAssert.AreEquivalent(new[] { "titleId", "helpId" }, errors.Select(error => error.FieldPath));
            foreach (MConfigValidationError error in errors)
            {
                Assert.AreEqual(typeof(MConfigReferenceTestAsset), error.ConfigType);
                Assert.AreEqual("panel", error.ConfigId);
                StringAssert.Contains(typeof(MConfigTestAsset).FullName, error.Message);
            }

            StringAssert.Contains("missing-title", errors.Single(error => error.FieldPath == "titleId").Message);
            StringAssert.Contains("missing-help", errors.Single(error => error.FieldPath == "helpId").Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyOptionalReferenceIsAllowed(string optionalId)
        {
            MConfig configs = new MConfig();
            configs.Register(CreateReference("panel", "title", optionalId));
            configs.Register(CreateConfig("title", 0));

            Assert.IsEmpty(configs.ValidateAll());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyRequiredReferenceReportsAFieldError(string requiredId)
        {
            MConfig configs = new MConfig();
            configs.Register(CreateReference("panel", requiredId, null));

            var errors = configs.ValidateAll();

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("titleId", errors[0].FieldPath);
            StringAssert.Contains(typeof(MConfigTestAsset).FullName, errors[0].Message);
            StringAssert.Contains("未填写有效 Id", errors[0].Message);
        }

        [Test]
        public void AnIdRegisteredUnderAnotherTypeDoesNotSatisfyAReference()
        {
            MConfig configs = new MConfig();
            configs.Register(CreateReference("shared", "shared", null));

            var errors = configs.ValidateAll();

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("titleId", errors[0].FieldPath);
            StringAssert.Contains(typeof(MConfigTestAsset).FullName, errors[0].Message);
            StringAssert.Contains("shared", errors[0].Message);
        }

        [Test]
        public void TheSameIdCanBeResolvedIndependentlyUnderDifferentTypes()
        {
            MConfigTestAsset target = CreateConfig("shared", 0);
            MConfigReferenceTestAsset source = CreateReference("shared", "shared", "shared");
            MConfig configs = new MConfig();
            configs.Register(source);
            configs.Register(target);

            Assert.AreSame(target, configs.Get<MConfigTestAsset>("shared"));
            Assert.AreSame(source, configs.Get<MConfigReferenceTestAsset>("shared"));
            Assert.IsEmpty(configs.ValidateAll());
        }

        [TestCase("TITLE")]
        [TestCase(" title ")]
        public void ReferenceIdsKeepOrdinalComparisonWithoutNormalization(string referenceId)
        {
            MConfig configs = new MConfig();
            configs.Register(CreateConfig("title", 0));
            configs.Register(CreateReference("panel", referenceId, null));

            Assert.AreEqual(1, configs.ValidateAll().Count);
        }

        [Test]
        public void TryResolveSupportsGenericAndExplicitTypesWithoutAddingErrors()
        {
            MConfigTestAsset target = CreateConfig("title", 0);
            MConfigProbeTestAsset source = CreateAsset<MConfigProbeTestAsset>("probe");
            source.OnValidateConfig = context =>
            {
                MConfigTestAsset typed;
                MConfigAsset untyped;
                Assert.IsTrue(context.TryResolve("title", out typed));
                Assert.AreSame(target, typed);
                Assert.IsTrue(context.TryResolve(typeof(MConfigTestAsset), "title", out untyped));
                Assert.AreSame(target, untyped);
                Assert.IsFalse(context.TryResolve("missing", out typed));
                Assert.IsNull(typed);
                Assert.IsFalse(context.TryResolve(null, out typed));
                Assert.IsNull(typed);
                Assert.IsFalse(context.TryResolve(typeof(MConfigTestAsset), " ", out untyped));
                Assert.IsNull(untyped);
                Assert.IsFalse(context.TryResolve(typeof(MConfigReferenceTestAsset), "title", out untyped));
                Assert.IsNull(untyped);
            };
            MConfig configs = new MConfig();
            configs.Register(source);
            configs.Register(target);

            Assert.IsEmpty(configs.ValidateAll());
        }

        [Test]
        public void ExplicitTypeReferenceAndBusinessErrorsPreserveNestedFieldPaths()
        {
            MConfigProbeTestAsset source = CreateAsset<MConfigProbeTestAsset>("group");
            source.OnValidateConfig = context =>
            {
                Assert.IsTrue(context.RequireReference(typeof(MConfigTestAsset), "entries[0].titleId", "title"));
                Assert.IsTrue(context.RequireReference<MConfigTestAsset>("entries[0].helpId", null, optional: true));
                Assert.IsFalse(context.RequireReference(typeof(MConfigTestAsset), "entries[1].titleId", "missing"));
                context.AddError("entries[1].weight", "权重必须大于零。");
            };
            MConfig configs = new MConfig();
            configs.Register(source);
            configs.Register(CreateConfig("title", 0));

            var errors = configs.ValidateAll();

            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual("entries[1].titleId", errors[0].FieldPath);
            StringAssert.StartsWith("MConfigProbeTestAsset[group].entries[1].titleId: ", errors[0].ToString());
            Assert.AreEqual("entries[1].weight", errors[1].FieldPath);
            Assert.AreEqual("权重必须大于零。", errors[1].Message);
        }

        [Test]
        public void ValidateAllAggregatesAcrossTypesAndStartsFreshAfterCorrections()
        {
            MConfigReferenceTestAsset first = CreateReference("first", "one", "two");
            MConfigReferenceTestAsset second = CreateReference("second", "one", null);
            MConfigTestAsset invalid = CreateConfig("one", -1);
            MConfig configs = new MConfig();
            configs.RegisterRange(new[] { first, second });

            var beforeRegistration = configs.ValidateAll();
            Assert.AreEqual(3, beforeRegistration.Count);

            configs.Register(invalid);
            var afterRegistration = configs.ValidateAll();
            Assert.AreEqual(2, afterRegistration.Count);
            CollectionAssert.AreEquivalent(new[] { "first", "one" }, afterRegistration.Select(error => error.ConfigId));

            configs.Register(CreateConfig("two", 0));
            invalid.Cost = 0;
            Assert.IsEmpty(configs.ValidateAll());
            Assert.AreEqual(3, beforeRegistration.Count);
            Assert.AreEqual(2, afterRegistration.Count);
        }

        [Test]
        public void DestroyedTargetIsMissingForBothReferenceApisAndOutputsRealNull()
        {
            MConfigTestAsset target = CreateConfig("title", 0);
            MConfigProbeTestAsset source = CreateAsset<MConfigProbeTestAsset>("panel");
            source.OnValidateConfig = context =>
            {
                MConfigTestAsset typed;
                MConfigAsset untyped;
                Assert.IsFalse(context.TryResolve("title", out typed));
                Assert.IsTrue(ReferenceEquals(null, typed));
                Assert.IsFalse(context.TryResolve(typeof(MConfigTestAsset), "title", out untyped));
                Assert.IsTrue(ReferenceEquals(null, untyped));
                Assert.IsFalse(context.RequireReference<MConfigTestAsset>("titleId", "title"));
                Assert.IsFalse(context.RequireReference(typeof(MConfigTestAsset), "helpId", "title", optional: true));
            };
            MConfig configs = new MConfig();
            configs.Register(target);
            configs.Register(source);
            UnityEngine.Object.DestroyImmediate(target);

            var errors = configs.ValidateAll();

            Assert.AreEqual(3, errors.Count);
            MConfigValidationError targetError = errors.Single(error => error.ConfigId == "title");
            Assert.AreEqual(typeof(MConfigTestAsset), targetError.ConfigType);
            Assert.IsNull(targetError.FieldPath);
            StringAssert.Contains("已被销毁", targetError.Message);
            CollectionAssert.AreEquivalent(new[] { "titleId", "helpId" },
                errors.Where(error => error.ConfigId == "panel").Select(error => error.FieldPath));
        }

        [Test]
        public void DestroyedSourceIsReportedWithoutCallingItsValidatorOrStoppingAggregation()
        {
            MConfigProbeTestAsset destroyed = CreateAsset<MConfigProbeTestAsset>("destroyed");
            MConfigProbeTestAsset alive = CreateAsset<MConfigProbeTestAsset>("alive");
            int destroyedCalls = 0;
            int aliveCalls = 0;
            destroyed.OnValidateConfig = context =>
            {
                destroyedCalls++;
                context.AddError(destroyed.name);
            };
            alive.OnValidateConfig = context =>
            {
                aliveCalls++;
                context.AddError("field", "后续配置仍然完成校验。");
            };
            MConfig configs = new MConfig();
            configs.RegisterRange(new[] { destroyed, alive });
            configs.Register(CreateConfig("invalid", -1));
            UnityEngine.Object.DestroyImmediate(destroyed);

            var errors = configs.ValidateAll();

            Assert.AreEqual(0, destroyedCalls);
            Assert.AreEqual(1, aliveCalls);
            Assert.AreEqual(3, errors.Count);
            CollectionAssert.AreEquivalent(new[] { "destroyed", "alive", "invalid" },
                errors.Select(error => error.ConfigId));
            MConfigValidationError sourceError = errors.Single(error => error.ConfigId == "destroyed");
            Assert.AreEqual(typeof(MConfigProbeTestAsset), sourceError.ConfigType);
            Assert.IsNull(sourceError.FieldPath);
            StringAssert.Contains("已被销毁", sourceError.Message);
        }

        [Test]
        public void ReferenceExistenceDoesNotRecursivelyValidateOrRejectCycles()
        {
            MConfigProbeTestAsset first = CreateAsset<MConfigProbeTestAsset>("first");
            MConfigProbeTestAsset second = CreateAsset<MConfigProbeTestAsset>("second");
            int firstCalls = 0;
            int secondCalls = 0;
            first.OnValidateConfig = context =>
            {
                firstCalls++;
                Assert.IsTrue(context.RequireReference<MConfigProbeTestAsset>("nextId", "second"));
            };
            second.OnValidateConfig = context =>
            {
                secondCalls++;
                Assert.IsTrue(context.RequireReference<MConfigProbeTestAsset>("nextId", "first"));
            };
            MConfig configs = new MConfig();
            configs.RegisterRange(new[] { first, second });

            Assert.IsEmpty(configs.ValidateAll());
            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls);
        }

        [Test]
        public void ReferenceLookupUsesTheRegistrationTypeRatherThanTheRuntimeType()
        {
            MConfigTestAsset target = CreateConfig("base-key", 0);
            MConfigProbeTestAsset source = CreateAsset<MConfigProbeTestAsset>("probe");
            source.OnValidateConfig = context =>
            {
                MConfigTestAsset derived;
                Assert.IsFalse(context.TryResolve("base-key", out derived));
                Assert.IsTrue(context.RequireReference<MConfigAsset>("targetId", "base-key"));
            };
            MConfig configs = new MConfig();
            configs.Register<MConfigAsset>(target);
            configs.Register(source);

            Assert.AreSame(target, configs.Get<MConfigAsset>("base-key"));
            Assert.IsEmpty(configs.ValidateAll());
        }

        [Test]
        public void InvalidApiArgumentsRemainProgrammingErrors()
        {
            MConfigProbeTestAsset source = CreateAsset<MConfigProbeTestAsset>("probe");
            source.OnValidateConfig = context =>
            {
                MConfigAsset ignored;
                Assert.Throws<ArgumentNullException>(() => context.TryResolve(null, "id", out ignored));
                Assert.Throws<ArgumentException>(() => context.TryResolve(typeof(string), "id", out ignored));
                Assert.Throws<ArgumentException>(() => context.RequireReference(typeof(string), "field", null, true));
                Assert.Throws<ArgumentException>(() => context.RequireReference<MConfigTestAsset>(" ", "id"));
                Assert.Throws<ArgumentException>(() => context.AddError(" "));
                Assert.Throws<ArgumentException>(() => context.AddError(" ", "message"));
                Assert.Throws<ArgumentException>(() => context.AddError("field", " "));
            };
            MConfig configs = new MConfig();
            configs.Register(source);

            Assert.IsEmpty(configs.ValidateAll());
        }

        [Test]
        public void ExistingRegistrationAndQueryFailureContractsAreUnchanged()
        {
            MConfig configs = new MConfig();
            MConfigTestAsset ignored;
            Assert.Throws<ArgumentNullException>(() => configs.Register<MConfigTestAsset>(null));
            Assert.Throws<MConfigRegistrationException>(() => configs.Register(CreateConfig(" ", 0)));
            Assert.Throws<ArgumentException>(() => configs.TryGet(" ", out ignored));
            Assert.Throws<ArgumentException>(() => configs.Get<MConfigTestAsset>(null));
            Assert.Throws<KeyNotFoundException>(() => configs.Get<MConfigTestAsset>("missing"));
            Assert.IsFalse(configs.TryGet("missing", out ignored));
            Assert.IsNull(ignored);

            configs.Register(CreateConfig("title", 0));
            configs.Clear();
            Assert.IsEmpty(configs.GetAll<MConfigTestAsset>());
            Assert.IsEmpty(configs.ValidateAll());
        }

        [Test]
        public void LegacyErrorConstructorPreservesItsMessageAndText()
        {
            MConfigValidationError error = new MConfigValidationError(typeof(MConfigTestAsset), "legacy", "原有错误。");

            Assert.IsNull(error.FieldPath);
            Assert.AreEqual("原有错误。", error.Message);
            Assert.AreEqual("MConfigTestAsset[legacy]: 原有错误。", error.ToString());
        }

        private MConfigTestAsset CreateConfig(string id, int cost)
        {
            MConfigTestAsset config = CreateAsset<MConfigTestAsset>(id);
            config.Cost = cost;
            return config;
        }

        private MConfigReferenceTestAsset CreateReference(string id, string titleId, string helpId)
        {
            MConfigReferenceTestAsset config = CreateAsset<MConfigReferenceTestAsset>(id);
            config.TitleId = titleId;
            config.HelpId = helpId;
            return config;
        }

        private TConfig CreateAsset<TConfig>(string id)
            where TConfig : MConfigAsset
        {
            TConfig config = ScriptableObject.CreateInstance<TConfig>();
            _createdAssets.Add(config);
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }
    }

    public sealed class MConfigTestAsset : MConfigAsset
    {
        public int Cost { get; set; }

        public override void Validate(MConfigValidationContext context)
        {
            if (Cost < 0)
            {
                context.AddError("金币必须大于或等于零。");
            }
        }
    }

    public sealed class MConfigReferenceTestAsset : MConfigAsset
    {
        public string TitleId { get; set; }
        public string HelpId { get; set; }

        public override void Validate(MConfigValidationContext context)
        {
            context.RequireReference<MConfigTestAsset>("titleId", TitleId);
            context.RequireReference<MConfigTestAsset>("helpId", HelpId, optional: true);
        }
    }

    public sealed class MConfigProbeTestAsset : MConfigAsset
    {
        public Action<MConfigValidationContext> OnValidateConfig { get; set; }

        public override void Validate(MConfigValidationContext context)
        {
            OnValidateConfig?.Invoke(context);
        }
    }
}
