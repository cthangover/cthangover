namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.TestRunner;

    /// <summary>Preserves ordinary plans and dependencies; externally gated launches require an exact selector.</summary>
    [Collection("runner-infrastructure")]
    public class RunnerSelectionTests : IDisposable
    {
        private readonly string failureReport = Path.Combine(Path.GetTempPath(),
            "cthangover-selection-" + Guid.NewGuid().ToString("N") + ".json");
        private readonly string catalogueRoot = Path.Combine(Path.GetTempPath(),
            "cthangover-selection-catalogue-" + Guid.NewGuid().ToString("N"));
        private string[] arguments = Array.Empty<string>();
        private List<IntegrationTestDefinition> catalog;

        /// <summary>Environment retention defaults remain intact and explicit CLI values still take precedence.</summary>
        [Fact]
        public void RetentionEnvironmentAndCliHaveDocumentedDefaultsAndPrecedence()
        {
            var names = new[]
            {
                "CTHANGOVER_TEST_RETENTION_DAYS",
                "CTHANGOVER_TEST_RETENTION_COUNT",
                "CTHANGOVER_TEST_RETENTION_GIB"
            };
            var previous = new string[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                previous[index] = Environment.GetEnvironmentVariable(names[index]);
            }

            try
            {
                foreach (var name in names)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }

                var defaults = RunnerOptions.Parse(Array.Empty<string>());
                Assert.Equal(3, defaults.RetentionCount);
                Assert.Equal(3, defaults.RetentionDays);
                Assert.Equal(20L * 1024 * 1024 * 1024, defaults.RetentionBytes);
                Environment.SetEnvironmentVariable(names[0], "2.5");
                Environment.SetEnvironmentVariable(names[1], "7");
                Environment.SetEnvironmentVariable(names[2], "0.5");
                var environment = RunnerOptions.Parse(Array.Empty<string>());
                Assert.Equal(2.5, environment.RetentionDays);
                Assert.Equal(7, environment.RetentionCount);
                Assert.Equal(512L * 1024 * 1024, environment.RetentionBytes);
                var cli = RunnerOptions.Parse(new[]
                {
                "--retention-days",
                "1",
                "--retention-count",
                "2",
                "--retention-gib",
                "3",
            });
                Assert.Equal(1, cli.RetentionDays);
                Assert.Equal(2, cli.RetentionCount);
                Assert.Equal(3L * 1024 * 1024 * 1024, cli.RetentionBytes);
            }
            finally
            {
                for (var index = 0; index < names.Length; index++)
                {
                    Environment.SetEnvironmentVariable(names[index], previous[index]);
                }
            }
        }

        /// <summary>Nonfinite, fractional integer and negative retention values cannot silently become defaults.</summary>
        [Theory]
        [InlineData("--retention-days", "NaN")]
        [InlineData("--retention-days", "-1")]
        [InlineData("--retention-count", "-1")]
        [InlineData("--retention-count", "2.5")]
        [InlineData("--retention-gib", "Infinity")]
        [InlineData("--retention-gib", "1e30")]
        public void InvalidRetentionValuesAreRejected(string option, string value)
        {
            arguments = new[]
            {
                option,
                value,
            };
            Assert.Throws<ArgumentException>(ParseOptions);
        }

        /// <summary>A dependent keeps its required setup in the same component even when setup was not explicitly selected.</summary>
        [Fact]
        public void SingleDependentAutomaticallyGetsSetupInTheSameEnvironment()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[]
            {
                "--test",
                "play",
            }));
            Assert.Equal(new[]
            {
                "setup",
                "play",
            }, Names(plan));
            Assert.Equal(plan[0].Environment, plan[1].Environment);
            Assert.Contains("play", plan[0].Reason);
        }

        /// <summary>Owner/renderer narrowing still includes necessary setup outside those filters.</summary>
        [Fact]
        public void OwnerAndRendererFiltersCannotDropNecessarySetup()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[]
            {
                "--mod",
                "rpg",
                "--renderer",
                "isometric",
            }));
            Assert.Equal(new[]
            {
                "setup",
                "play",
            }, Names(plan));
        }

        /// <summary>Case filtering selects the requested mod's unit launch without expanding unrelated integration work.</summary>
        [Fact]
        public void ACaseFilterTargetsOnlyTheRequestedMod()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[]
            {
                "--mod",
                "rpg",
                "--filter",
                "followers/*",
            }));
            Assert.Equal("mod_units:rpg", Assert.Single(plan).Test.Name);
            Assert.Equal("followers/*", plan[0].Filter);
        }

        /// <summary>Repeated glob union does not duplicate prerequisites or combine unrelated component state.</summary>
        [Fact]
        public void RepeatedGlobsAreAUnionWithoutDuplicatePrerequisites()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[]
            {
                "--test",
                "p*",
                "--test",
                "setup",
                "--test",
                "other",
            }));
            Assert.Equal(3, plan.Count);
            var setupCount = 0;
            foreach (var entry in plan)
            {
                if (entry.Test.Name == "setup")
                {
                    setupCount++;
                }
            }

            Assert.Equal(1, setupCount);
            Assert.NotEqual(Entry(plan, "play").Environment, Entry(plan, "other").Environment);
        }

        /// <summary>Unknown selectors remain errors instead of generating an empty successful run.</summary>
        [Theory]
        [InlineData("--test", "missing")]
        [InlineData("--profile", "missing")]
        [InlineData("--mod", "missing")]
        [InlineData("--renderer", "missing")]
        public void UnknownOrEmptySelectionIsAnError(string option, string value)
        {
            arguments = new[]
            {
                option,
                value,
            };
            catalog = Catalog();
            Assert.Throws<ArgumentException>(BuildPlan);
        }

        /// <summary>The fast profile retains the explicit repository compiler exclusion in its unit child filter.</summary>
        [Fact]
        public void FastProfileExcludesTheRepositoryCompileGuard()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[]
            {
                "--profile",
                "fast",
            }));
            Assert.Equal("unit", Assert.Single(plan).Test.Name);
            Assert.Contains("!~ToolsModCompileTests", plan[0].Filter);
        }

        /// <summary>A selected cyclic prerequisite fails before any process starts.</summary>
        [Fact]
        public void CyclesAreRejectedBeforeExecution()
        {
            catalog = Catalog();
            Find(catalog, "setup").DependsOn = new[] { "play" };
            arguments = new[]
            {
                "--test",
                "play",
            };
            Assert.Throws<InvalidOperationException>(BuildPlan);
        }

        /// <summary>A missing option value must never consume another option as a value.</summary>
        [Fact]
        public void AValueCannotSilentlyConsumeTheNextOption()
        {
            arguments = new[]
            {
                "--test",
                "--list",
            };
            Assert.Throws<ArgumentException>(ParseOptions);
        }

        /// <summary>Unselected dependents cannot join selected launches into one stateful component.</summary>
        [Fact]
        public void AnUnselectedDependentDoesNotMakeSelectedTestsShareState()
        {
            var source = Catalog();
            source.Add(new IntegrationTestDefinition
            {
                Name = "unselected",
                DependsOn = new[]
                {
                    "play",
                    "other"
                }
            });
            var plan = TestPlanner.Build(source, RunnerOptions.Parse(new[]
            {
                "--test",
                "play",
                "--test",
                "other",
            }));
            Assert.NotEqual(Entry(plan, "play").Environment, Entry(plan, "other").Environment);
        }

        /// <summary>A selected case filter does not restrict the independent prerequisite setup.</summary>
        [Fact]
        public void SelectedCaseFilterDoesNotFilterItsPreparation()
        {
            var source = Catalog();
            Find(source, "mod_units:rpg").DependsOn = new[] { "setup" };
            var plan = TestPlanner.Build(source, RunnerOptions.Parse(new[]
            {
                "--mod",
                "rpg",
                "--filter",
                "followers/*",
            }));
            Assert.Null(plan[0].Filter);
            Assert.Equal("followers/*", plan[1].Filter);
        }

        /// <summary>The final execution plan retains every serialized field after a full JSON cycle.</summary>
        [Fact]
        public void ExecutionPlanSurvivesJsonRoundTrip()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[]
            {
                "--mod",
                "rpg",
                "--filter",
                "followers/*",
            }));
            var json = JsonSerializer.Serialize(plan, ReportWriter.JsonOptions);
            var copy = JsonSerializer.Deserialize<List<TestPlanEntry>>(json, ReportWriter.JsonOptions);
            Assert.Equal(json, JsonSerializer.Serialize(copy, ReportWriter.JsonOptions));
        }

        /// <summary>Sequential scheduling keeps the selected plan, prerequisite order and state boundaries unchanged.</summary>
        [Fact]
        public void OneJobKeepsTheOriginalPlanAndOrder()
        {
            var original = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[]
            {
                "--test",
                "play",
                "--test",
                "other",
            }));
            var sequentialOptions = RunnerOptions.Parse(new[]
            {
                "--test",
                "play",
                "--test",
                "other",
                "--jobs",
                "1",
            });
            var sequential = TestPlanner.Build(Catalog(), sequentialOptions);
            Assert.Equal(JsonSerializer.Serialize(original, ReportWriter.JsonOptions),
                JsonSerializer.Serialize(sequential, ReportWriter.JsonOptions));
            Assert.Equal(new[]
            {
                "setup",
                "play",
                "other",
            }, Names(sequential));
        }

        /// <summary>Invalid worker budgets refuse instead of silently altering concurrency.</summary>
        [Theory]
        [InlineData("--jobs", "0")]
        [InlineData("--gui-jobs", "-1")]
        [InlineData("--write-jobs", "no")]
        public void ConcurrencyLimitsMustBePositive(string option, string value)
        {
            arguments = new[]
            {
                option,
                value,
            };
            Assert.Throws<ArgumentException>(ParseOptions);
        }

        /// <summary>An unselected joint dependent cannot authorize overlapping legacy output scopes.</summary>
        [Fact]
        public void UnselectedDependentCannotAuthorizeOverlappingOutputDirectories()
        {
            catalog = Catalog();
            catalog.Add(new IntegrationTestDefinition
            {
                Name = "unselected",
                DependsOn = new[]
                {
                    "play",
                    "other"
                }
            });
            var names = new[]
            {
                "play",
                "other",
            };
            foreach (var name in names)
            {
                var test = Find(catalog, name);
                test.Project = "output";
                test.Args = new[] { "--shots=res://tests/artifacts/shared" };
            }

            ProjectAccess.ValidateOutputs(catalog);
            arguments = new[]
            {
                "--test",
                "play",
                "--test",
                "other",
            };
            Assert.Throws<InvalidOperationException>(BuildPlan);
        }

        /// <summary>Integrity retries retain original candidate filters and recover their prerequisite setup.</summary>
        [Fact]
        public void SharedIntegrityFailureRepeatsCandidatesWithTheirFiltersAndPrerequisites()
        {
            var source = Catalog();
            Find(source, "mod_units:rpg").DependsOn = new[] { "setup" };
            var report = new RunReport
            {
                Results = new()
                {
                    new TestResult
                    {
                        Name = "mod_units:rpg",
                        Passed = true,
                        PassedChecks = 1,
                        Filter = "followers/*"
                    },
                    new TestResult
                    {
                        Name = "shared-project-integrity",
                        Kind = "infrastructure",
                        CandidateLaunches = new[] { "mod_units:rpg" }
                    }
                }
            };
            File.WriteAllText(failureReport, JsonSerializer.Serialize(report, ReportWriter.JsonOptions));
            var plan = TestPlanner.Build(source, RunnerOptions.Parse(new[]
            {
                "--failed",
                failureReport,
            }));
            Assert.Equal(new[]
            {
                "setup",
                "mod_units:rpg",
            }, Names(plan));
            Assert.Null(plan[0].Filter);
            Assert.Equal("followers/*", plan[1].Filter);
        }

        /// <summary>Adding an externally gated case cannot change ordinary default/profile/tag/glob launch plans.</summary>
        [Theory]
        [InlineData("default")]
        [InlineData("profile")]
        [InlineData("tag")]
        [InlineData("glob")]
        public void ExplicitAdmissionCannotBeGrantedByBroadSelection(string selector)
        {
            var source = Catalog();
            Find(source, "setup").Tags = new[] { "ordinary" };
            var options = new RunnerOptions();
            if (selector == "profile")
            {
                options = RunnerOptions.Parse(new[]
                {
                "--profile",
                "full",
            });
            }
            else if (selector == "tag")
            {
                options = RunnerOptions.Parse(new[]
                {
                "--tag",
                "ordinary",
            });
            }
            else if (selector == "glob")
            {
                options = RunnerOptions.Parse(new[]
                {
                "--test",
                "*",
            });
            }
            else
            {
                options = RunnerOptions.Parse(Array.Empty<string>());
            }

            var baseline = TestPlanner.Build(source, options);
            source.Add(Gated());
            var guarded = TestPlanner.Build(source, options);
            Assert.Equal(JsonSerializer.Serialize(baseline, ReportWriter.JsonOptions),
                JsonSerializer.Serialize(guarded, ReportWriter.JsonOptions));
        }

        /// <summary>Only an exact catalogue selector admits an external gate; dependency closure cannot bypass that consent.</summary>
        [Fact]
        public void ExactSelectionAdmitsGateButAnImplicitPrerequisiteRefuses()
        {
            catalog = Catalog();
            catalog.Add(Gated());
            var admitted = TestPlanner.Build(catalog, RunnerOptions.Parse(new[]
            {
                "--test",
                "external_gate",
            }));
            Assert.Equal("external_gate", Assert.Single(admitted).Test.Name);
            Find(catalog, "play").DependsOn = new[] { "external_gate" };
            arguments = new[]
            {
                "--test",
                "play",
            };
            var refusal = Assert.Throws<ArgumentException>(BuildPlan);
            Assert.Contains("exact --test selection is required", refusal.Message);
            var explicitChain = TestPlanner.Build(catalog, RunnerOptions.Parse(new[]
            {
                "--test",
                "play",
                "--test",
                "external_gate",
            }));
            Assert.Equal(new[]
            {
                "external_gate",
                "play",
            }, Names(explicitChain));
        }

        /// <summary>A failed report cannot automatically re-admit an externally controlled gate on a later invocation.</summary>
        [Fact]
        public void FailedReportDoesNotGrantExplicitAdmission()
        {
            catalog = Catalog();
            catalog.Add(Gated());
            var report = new RunReport
            {
                Results = new()
                {
                    new TestResult
                    {
                        Name = "external_gate",
                        Passed = false
                    }
                }
            };
            File.WriteAllText(failureReport, JsonSerializer.Serialize(report, ReportWriter.JsonOptions));
            arguments = new[]
            {
                "--failed",
                failureReport,
            };
            Assert.Throws<ArgumentException>(BuildPlan);
        }

        /// <summary>Persisted admission flags round-trip; ordinary definitions remain implicit by default.</summary>
        [Fact]
        public void AdmissionFlagRoundTripsAndOrdinaryDefinitionsRemainImplicit()
        {
            Assert.False(new IntegrationTestDefinition().RequiresExplicitSelection);
            var original = Gated();
            var json = JsonSerializer.Serialize(original, ReportWriter.JsonOptions);
            var restored = JsonSerializer.Deserialize<IntegrationTestDefinition>(json, ReportWriter.JsonOptions);
            Assert.NotNull(restored);
            Assert.True(restored.RequiresExplicitSelection);
            Assert.Equal(json, JsonSerializer.Serialize(restored, ReportWriter.JsonOptions));
        }

        /// <summary>
        /// Scheduled suite admission survives real manifest loading and generated aliases, preserving ordinary broad plans.
        /// Only the exact scheduled name or exact derived alias can authorize the externally controlled suite.
        /// </summary>
        [Theory]
        [InlineData(null, null, null)]
        [InlineData("--profile", "full", null)]
        [InlineData("--kind", "mod-unit", null)]
        [InlineData("--mod", "review_selection_base", null)]
        [InlineData("--test", "*", null)]
        [InlineData("--test", "fixture_guarded", "fixture_guarded")]
        [InlineData("--test", "mod_units:review_selection_base__guarded", "mod_units:review_selection_base__guarded")]
        public void RealCatalogueAliasesRequireExactAdmission(string option, string value, string admitted)
        {
            var ordinary = RealCatalogue(false);
            var guarded = RealCatalogue(true);
            Assert.True(Find(guarded, "fixture_guarded").RequiresExplicitSelection);
            Assert.True(Find(guarded, "mod_units:review_selection_base__guarded").RequiresExplicitSelection);
            Assert.False(Find(guarded, "fixture_ordinary").RequiresExplicitSelection);
            Assert.False(Find(guarded, "mod_units:review_selection_base__ordinary").RequiresExplicitSelection);
            var options = RunnerOptions.Parse(option == null ? Array.Empty<string>() : new[]
            {
                option,
                value
            });
            var plan = TestPlanner.Build(guarded, options);
            if (admitted != null)
            {
                var test = Assert.Single(plan).Test;
                Assert.Equal(admitted, test.Name);
                Assert.True(test.RequiresExplicitSelection);
                Assert.Equal("write", test.Project);
                Assert.True(test.Windowed);
                return;
            }

            var baseline = TestPlanner.Build(ordinary, options);
            Assert.Equal(JsonSerializer.Serialize(baseline, ReportWriter.JsonOptions),
                JsonSerializer.Serialize(plan, ReportWriter.JsonOptions));
        }

        private List<IntegrationTestDefinition> RealCatalogue(bool guarded)
        {
            var root = Path.Combine(catalogueRoot, "mods_private", "review_selection_base");
            var suites = new JsonArray();
            var manifest = new TestManifest
            {
                SchemaVersion = 2,
                Unit = new UnitManifest { Project = "tests/unit.csproj" }
            };
            foreach (var id in guarded ? new[] { "ordinary", "guarded" } : new[] { "ordinary" })
            {
                var directory = Path.Combine(root, "test_suites", id);
                Directory.CreateDirectory(directory);
                suites.Add(new JsonObject
                {
                    ["id"] = id,
                    ["activate"] = new JsonArray("--itest=fixture_" + id)
                });
                File.WriteAllText(Path.Combine(directory, "manifest.json"), new JsonObject
                {
                    ["id"] = "review_selection_base__" + id,
                    ["test_sources"] = new JsonArray("tests/*.cs")
                }.ToJsonString());
                manifest.Integration.Add(new IntegrationTestDefinition
                {
                    Name = "fixture_" + id,
                    Kind = "mod-unit",
                    Scene = "res://scenes/menu/launcher.tscn",
                    Args = new[]
                    {
                        "--",
                        "--frontend",
                        "--itest=fixture_" + id,
                        "--run-tests",
                        "--mod=review_selection_base__" + id
                    },
                    Result = new ResultContract
                    {
                        Name = "mod_units",
                        ChildPrefix = "mod_units:"
                    },
                    Owners = new[] { "review_selection_base" },
                    Profiles = new[] { "full" },
                    Project = "write",
                    Windowed = true,
                    RequiresExplicitSelection = id == "guarded"
                });
            }

            File.WriteAllText(Path.Combine(root, "manifest.json"), new JsonObject
            {
                ["id"] = "review_selection_base",
                ["test_suites"] = suites
            }.ToJsonString());
            var tests = Path.Combine(catalogueRoot, "tests");
            Directory.CreateDirectory(tests);
            File.WriteAllText(Path.Combine(tests, "tests.manifest.json"), JsonSerializer.Serialize(manifest, ReportWriter.JsonOptions));
            return TestCatalog.Load(catalogueRoot, TestManifest.LoadProject(catalogueRoot));
        }

        private void ParseOptions()
        {
            RunnerOptions.Parse(arguments);
        }

        private void BuildPlan()
        {
            TestPlanner.Build(catalog, RunnerOptions.Parse(arguments));
        }

        private static IntegrationTestDefinition Gated()
        {
            return new IntegrationTestDefinition
            {
                Name = "external_gate",
                RequiresExplicitSelection = true,
                Owners = new[] { "tools" },
                Profiles = new[] { "full" },
                Tags = new[] { "ordinary" }
            };
        }

        private static IntegrationTestDefinition Find(List<IntegrationTestDefinition> source, string name)
        {
            foreach (var item in source)
            {
                if (item.Name == name)
                {
                    return item;
                }
            }

            throw new InvalidOperationException("Missing test fixture: " + name);
        }

        private static TestPlanEntry Entry(List<TestPlanEntry> plan, string name)
        {
            foreach (var entry in plan)
            {
                if (entry.Test.Name == name)
                {
                    return entry;
                }
            }

            throw new InvalidOperationException("Missing planned fixture: " + name);
        }

        private static string[] Names(List<TestPlanEntry> plan)
        {
            var names = new string[plan.Count];
            for (var index = 0; index < plan.Count; index++)
            {
                names[index] = plan[index].Test.Name;
            }

            return names;
        }

        private static List<IntegrationTestDefinition> Catalog()
        {
            return new()
            {
                new()
                {
                    Name = "unit",
                    Kind = "unit",
                    Profiles = new[]
                    {
                        "full",
                        "fast"
                    },
                    Owners = new[] { "core" }
                },
                new()
                {
                    Name = "setup",
                    Owners = new[] { "tools" },
                    Profiles = new[] { "full" }
                },
                new()
                {
                    Name = "play",
                    Owners = new[] { "rpg" },
                    Profiles = new[] { "full" },
                    DependsOn = new[] { "setup" },
                    Renderer = "isometric"
                },
                new()
                {
                    Name = "other",
                    Owners = new[] { "items_base" },
                    Profiles = new[] { "full" }
                },
                new()
                {
                    Name = "mod_units:rpg",
                    Kind = "mod-unit",
                    Owners = new[] { "rpg" },
                    Profiles = new[]
                    {
                        "full",
                        "fast"
                    },
                    Tags = new[] { "individual" }
                }
            };
        }

        /// <summary>Removes this fixture's report and tiny manifest tree; environment mutations restore their own values.</summary>
        public void Dispose()
        {
            File.Delete(failureReport);
            if (Directory.Exists(catalogueRoot))
            {
                Directory.Delete(catalogueRoot, true);
            }
        }
    }
}
