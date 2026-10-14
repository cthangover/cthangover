using System;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Marks a static method inside a mod's <c>test_sources</c> as a mod unit
    /// test. Tests are discovered by <see cref="ModTestDiscovery"/> through
    /// reflection in the separately compiled <c>{modId}__tests</c> assembly —
    /// the production pipeline never compiles or loads them (see the
    /// "tests are off the player's path" contract in plan-mod-testing.md §3.1).
    ///
    /// The method must be static, take a single <see cref="ModTestContext"/>
    /// parameter and return either <c>void</c> or <c>Task</c>. The test name
    /// comes from the attribute; failures are reported by throwing —
    /// typically via <see cref="ModAssert"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ModTestAttribute : Attribute
    {
        /// <summary>Test name used in reports; conventionally "group/case".</summary>
        public string Name { get; }

        /// <summary>
        /// Per-test timeout in seconds for async tests. A synchronous test
        /// cannot be aborted in-process, so the timeout applies to the
        /// returned <c>Task</c> only; hangs in sync tests are contained by
        /// the per-process isolation of the CI fan-out.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Optional skip reason; when non-empty the test is reported as Skipped without running.</summary>
        public string Skip { get; set; }

        /// <summary>Declares a test with the given report name.</summary>
        public ModTestAttribute(string name)
        {
            Name = name;
        }
    }
}
