using BalloonParty.UI.Telemetry;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Tests.UI
{
    // Scoped to the one branch this file can safely exercise — the guard clause when no MetricLabel
    // exists under the scope. Mirrors RegisterAudioTests' registration-existence-only pattern (never
    // Build()s).
    //
    // The symmetric "found" branch (>=1 label -> registers MetricValueResolver + MetricLabelBinder)
    // needs a real MetricLabel, which needs a TMP_Text ([RequireComponent]) that
    // BalloonParty.Tests.EditMode.asmdef has no reference to (see MetricLabelIsZeroTests) — flagged in
    // the W4 coverage review rather than worked around here. The GameObject stays inactive throughout so
    // LifetimeScope.Awake() (which calls Build() by default) never runs — this test only needs
    // GetComponentsInChildren to see an empty result, not a built container.
    [TestFixture]
    public class MetricLabelRegistrationTests
    {
        [Test]
        public void RegisterMetricLabels_NoLabelsUnderScope_RegistersNothing()
        {
            var scopeGo = new GameObject("Scope");
            scopeGo.SetActive(false);

            try
            {
                var scope = scopeGo.AddComponent<LifetimeScope>();
                var builder = new ContainerBuilder();

                builder.RegisterMetricLabels(scope);

                // The early return must skip both registrations together — a half-registered graph
                // (e.g. the resolver registered but the binder not) would null-ref the first time
                // anything actually resolved MetricLabelBinder.
                Assert.IsFalse(builder.Exists(typeof(MetricValueResolver), includeInterfaceTypes: true));
                Assert.IsFalse(builder.Exists(typeof(MetricLabelBinder), includeInterfaceTypes: true));
            }
            finally
            {
                Object.DestroyImmediate(scopeGo);
            }
        }
    }
}
