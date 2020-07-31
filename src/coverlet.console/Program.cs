﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using ConsoleTables;
using Coverlet.Console.Custom;
using Coverlet.Console.Logging;
using Coverlet.Core;
using Coverlet.Core.Abstractions;
using Coverlet.Core.Enums;
using Coverlet.Core.Helpers;
using Coverlet.Core.Reporters;
using Coverlet.Core.Symbols;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Coverlet.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            args = new string[]
            {
                //"injector",@"D:\Projects\C#\CodeCoverage2\CodeCoverage2\bin\Debug\netcoreapp3.1\TestLibrary.dll","--include-test-assembly"
                "collector", @"D:\github\coverlet-coverage\coverlet\src\coverlet.console\bin\Debug\netcoreapp2.2\CoverageResult_4ec76d37-ecc6-4917-a7b4-2f390dbf4a99","--format", "opencover"
            };
            if (args[0].ToLower() == "injector")
            {
                Injector(args.Skip(1).ToArray());
            }
            else if (args[0].ToLower() == "collector")
            {
                Collector(args.Skip(1).ToArray());
            }
            else
            {
                System.Console.WriteLine($"Invalid type {args[0]}. (injector|collector)");
            }
        }

        static int Injector(string[] args)
        {
            //args = new string[]{ @"D:\Projects\C#\CodeCoverage2\CodeCoverage2\bin\Debug\netcoreapp3.1\TestLibrary.dll",
            //    "--include-test-assembly",
            //    "--format",
            //    "opencover"
            //    };
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddTransient<IRetryHelper, RetryHelper>();
            serviceCollection.AddTransient<IProcessExitHandler, ProcessExitHandler>();
            serviceCollection.AddTransient<IFileSystem, FileSystem>();
            serviceCollection.AddTransient<ILogger, ConsoleLogger>();
            // We need to keep singleton/static semantics
            serviceCollection.AddSingleton<IInstrumentationHelper, CustomInstrumentationHelper>();
            serviceCollection.AddSingleton<ISourceRootTranslator, SourceRootTranslator>(provider => new SourceRootTranslator(provider.GetRequiredService<ILogger>(), provider.GetRequiredService<IFileSystem>()));
            serviceCollection.AddSingleton<ICecilSymbolHelper, CecilSymbolHelper>();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            var logger = (ConsoleLogger)serviceProvider.GetService<ILogger>();
            var fileSystem = serviceProvider.GetService<IFileSystem>();

            var app = new CommandLineApplication();
            app.Name = "coverlet";
            app.FullName = "Cross platform .NET Core code coverage tool";
            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", GetAssemblyVersion());
            int exitCode = (int)CommandExitCodes.Success;

            CommandArgument module = app.Argument("<ASSEMBLY>", "Path to the test assembly.");
            CommandOption<LogLevel> verbosity = app.Option<LogLevel>("-v|--verbosity", "Sets the verbosity level of the command. Allowed values are quiet, minimal, normal, detailed.", CommandOptionType.SingleValue);
            CommandOption excludeFilters = app.Option("--exclude", "Filter expressions to exclude specific modules and types.", CommandOptionType.MultipleValue);
            CommandOption includeFilters = app.Option("--include", "Filter expressions to include only specific modules and types.", CommandOptionType.MultipleValue);
            CommandOption excludedSourceFiles = app.Option("--exclude-by-file", "Glob patterns specifying source files to exclude.", CommandOptionType.MultipleValue);
            CommandOption includeDirectories = app.Option("--include-directory", "Include directories containing additional assemblies to be instrumented.", CommandOptionType.MultipleValue);
            CommandOption excludeAttributes = app.Option("--exclude-by-attribute", "Attributes to exclude from code coverage.", CommandOptionType.MultipleValue);
            CommandOption includeTestAssembly = app.Option("--include-test-assembly", "Specifies whether to report code coverage of the test assembly.", CommandOptionType.NoValue);
            CommandOption singleHit = app.Option("--single-hit", "Specifies whether to limit code coverage hit reporting to a single hit for each location", CommandOptionType.NoValue);
            CommandOption mergeWith = app.Option("--merge-with", "Path to existing coverage result to merge.", CommandOptionType.SingleValue);
            CommandOption useSourceLink = app.Option("--use-source-link", "Specifies whether to use SourceLink URIs in place of file system paths.", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                if (string.IsNullOrEmpty(module.Value) || string.IsNullOrWhiteSpace(module.Value))
                    throw new CommandParsingException(app, "No test assembly specified.");

                if (verbosity.HasValue())
                {
                    // Adjust log level based on user input.
                    logger.Level = verbosity.ParsedValue;
                }

                CoverageParameters parameters = new CoverageParameters
                {
                    IncludeFilters = includeFilters.Values.ToArray(),
                    IncludeDirectories = includeDirectories.Values.ToArray(),
                    ExcludeFilters = excludeFilters.Values.ToArray(),
                    ExcludedSourceFiles = excludedSourceFiles.Values.ToArray(),
                    ExcludeAttributes = excludeAttributes.Values.ToArray(),
                    IncludeTestAssembly = includeTestAssembly.HasValue(),
                    SingleHit = singleHit.HasValue(),
                    MergeWith = mergeWith.Value(),
                    UseSourceLink = useSourceLink.HasValue()
                };

                Coverage coverage = new Coverage(module.Value,
                                                 parameters,
                                                 logger,
                                                 serviceProvider.GetRequiredService<IInstrumentationHelper>(),
                                                 fileSystem,
                                                 serviceProvider.GetRequiredService<ISourceRootTranslator>(),
                                                 serviceProvider.GetRequiredService<ICecilSymbolHelper>());

                // inject to the test dll
                CoveragePrepareResult coveragePrepareResult = coverage.PrepareModules();

                // store the result
                using (Stream fs = fileSystem.NewFileStream($"CoverageResult_{Guid.NewGuid()}", FileMode.Create, FileAccess.Write))
                using (Stream stream = CoveragePrepareResult.Serialize(coveragePrepareResult))
                {
                    stream.CopyTo(fs);
                }

                return exitCode;
            });

            return app.Execute(args);
        }

        static int Collector(string[] args)
        {
            //args = new string[]{ @"D:\Projects\C#\CodeCoverage2\CodeCoverage2\bin\Debug\netcoreapp3.1\TestLibrary.dll",
            //    "--format",
            //    "opencover"
            //    };
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddTransient<IRetryHelper, RetryHelper>();
            serviceCollection.AddTransient<IProcessExitHandler, ProcessExitHandler>();
            serviceCollection.AddTransient<IFileSystem, FileSystem>();
            serviceCollection.AddTransient<ILogger, ConsoleLogger>();
            // We need to keep singleton/static semantics
            serviceCollection.AddSingleton<IInstrumentationHelper, CustomInstrumentationHelper>();
            serviceCollection.AddSingleton<ISourceRootTranslator, SourceRootTranslator>(provider => new SourceRootTranslator(provider.GetRequiredService<ILogger>(), provider.GetRequiredService<IFileSystem>()));
            serviceCollection.AddSingleton<ICecilSymbolHelper, CecilSymbolHelper>();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            var logger = (ConsoleLogger)serviceProvider.GetService<ILogger>();
            var fileSystem = serviceProvider.GetService<IFileSystem>();

            var app = new CommandLineApplication();
            app.Name = "coverlet";
            app.FullName = "Cross platform .NET Core code coverage tool";
            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", GetAssemblyVersion());
            int exitCode = (int)CommandExitCodes.Success;

            CommandArgument prepareResult = app.Argument("<RESULT FILE>", "Path to the test coverage result file.");
            CommandOption output = app.Option("-o|--output", "Output of the generated coverage report", CommandOptionType.SingleValue);
            CommandOption formats = app.Option("-f|--format", "Format of the generated coverage report.", CommandOptionType.MultipleValue);
            CommandOption threshold = app.Option("--threshold", "Exits with error if the coverage % is below value.", CommandOptionType.SingleValue);
            CommandOption thresholdTypes = app.Option("--threshold-type", "Coverage type to apply the threshold to.", CommandOptionType.MultipleValue);
            CommandOption thresholdStat = app.Option("--threshold-stat", "Coverage statistic used to enforce the threshold value.", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                // collect the coverage result
                CoveragePrepareResult coveragePrepareResult = null;
                using (Stream fs = fileSystem.NewFileStream(prepareResult.Value, FileMode.Open, FileAccess.Read))
                {
                    coveragePrepareResult = CoveragePrepareResult.Deserialize(fs);
                }

                Coverage coverage = new Coverage(coveragePrepareResult, logger,
                                                     serviceProvider.GetRequiredService<IInstrumentationHelper>(),
                                                     fileSystem,
                                                     serviceProvider.GetRequiredService<ISourceRootTranslator>());

                var dOutput = output.HasValue() ? output.Value() : Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString();
                var dThreshold = threshold.HasValue() ? double.Parse(threshold.Value()) : 0;
                var dThresholdTypes = thresholdTypes.HasValue() ? thresholdTypes.Values : new List<string>(new string[] { "line", "branch", "method" });
                var dThresholdStat = thresholdStat.HasValue() ? Enum.Parse<ThresholdStatistic>(thresholdStat.Value(), true) : Enum.Parse<ThresholdStatistic>("minimum", true);

                logger.LogInformation("\nCalculating coverage result...");

                CoverageResult result = coverage.GetCoverageResult();
                var directory = Path.GetDirectoryName(dOutput);
                if (directory == string.Empty)
                {
                    directory = Directory.GetCurrentDirectory();
                }
                else if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                foreach (var format in (formats.HasValue() ? formats.Values : new List<string>(new string[] { "json" })))
                {
                    var reporter = new ReporterFactory(format).CreateReporter();
                    if (reporter == null)
                    {
                        throw new Exception($"Specified output format '{format}' is not supported");
                    }

                    if (reporter.OutputType == ReporterOutputType.Console)
                    {
                        // Output to console
                        logger.LogInformation("  Outputting results to console", important: true);
                        logger.LogInformation(reporter.Report(result), important: true);
                    }
                    else
                    {
                        // Output to file
                        var filename = Path.GetFileName(dOutput);
                        filename = (filename == string.Empty) ? $"coverage.{reporter.Extension}" : filename;
                        filename = Path.HasExtension(filename) ? filename : $"{filename}.{reporter.Extension}";

                        var report = Path.Combine(directory, filename);
                        logger.LogInformation($"  Generating report '{report}'", important: true);
                        fileSystem.WriteAllText(report, reporter.Report(result));
                    }
                }


                var thresholdTypeFlags = ThresholdTypeFlags.None;

                foreach (var thresholdType in dThresholdTypes)
                {
                    if (thresholdType.Equals("line", StringComparison.OrdinalIgnoreCase))
                    {
                        thresholdTypeFlags |= ThresholdTypeFlags.Line;
                    }
                    else if (thresholdType.Equals("branch", StringComparison.OrdinalIgnoreCase))
                    {
                        thresholdTypeFlags |= ThresholdTypeFlags.Branch;
                    }
                    else if (thresholdType.Equals("method", StringComparison.OrdinalIgnoreCase))
                    {
                        thresholdTypeFlags |= ThresholdTypeFlags.Method;
                    }
                }

                var coverageTable = new ConsoleTable("Module", "Line", "Branch", "Method");
                var summary = new CoverageSummary();
                int numModules = result.Modules.Count;

                var linePercentCalculation = summary.CalculateLineCoverage(result.Modules);
                var branchPercentCalculation = summary.CalculateBranchCoverage(result.Modules);
                var methodPercentCalculation = summary.CalculateMethodCoverage(result.Modules);

                var totalLinePercent = linePercentCalculation.Percent;
                var totalBranchPercent = branchPercentCalculation.Percent;
                var totalMethodPercent = methodPercentCalculation.Percent;

                var averageLinePercent = linePercentCalculation.AverageModulePercent;
                var averageBranchPercent = branchPercentCalculation.AverageModulePercent;
                var averageMethodPercent = methodPercentCalculation.AverageModulePercent;

                foreach (var _module in result.Modules)
                {
                    var linePercent = summary.CalculateLineCoverage(_module.Value).Percent;
                    var branchPercent = summary.CalculateBranchCoverage(_module.Value).Percent;
                    var methodPercent = summary.CalculateMethodCoverage(_module.Value).Percent;

                    coverageTable.AddRow(Path.GetFileNameWithoutExtension(_module.Key), $"{linePercent}%", $"{branchPercent}%", $"{methodPercent}%");
                }

                logger.LogInformation(coverageTable.ToStringAlternative());

                coverageTable.Columns.Clear();
                coverageTable.Rows.Clear();

                coverageTable.AddColumn(new[] { "", "Line", "Branch", "Method" });
                coverageTable.AddRow("Total", $"{totalLinePercent}%", $"{totalBranchPercent}%", $"{totalMethodPercent}%");
                coverageTable.AddRow("Average", $"{averageLinePercent}%", $"{averageBranchPercent}%", $"{averageMethodPercent}%");

                logger.LogInformation(coverageTable.ToStringAlternative());
                //if (process.ExitCode > 0)
                //{
                //    exitCode += (int)CommandExitCodes.TestFailed;
                //}
                thresholdTypeFlags = result.GetThresholdTypesBelowThreshold(summary, dThreshold, thresholdTypeFlags, dThresholdStat);
                if (thresholdTypeFlags != ThresholdTypeFlags.None)
                {
                    exitCode += (int)CommandExitCodes.CoverageBelowThreshold;
                    var exceptionMessageBuilder = new StringBuilder();
                    if ((thresholdTypeFlags & ThresholdTypeFlags.Line) != ThresholdTypeFlags.None)
                    {
                        exceptionMessageBuilder.AppendLine($"The {dThresholdStat.ToString().ToLower()} line coverage is below the specified {dThreshold}");
                    }

                    if ((thresholdTypeFlags & ThresholdTypeFlags.Branch) != ThresholdTypeFlags.None)
                    {
                        exceptionMessageBuilder.AppendLine($"The {dThresholdStat.ToString().ToLower()} branch coverage is below the specified {dThreshold}");
                    }

                    if ((thresholdTypeFlags & ThresholdTypeFlags.Method) != ThresholdTypeFlags.None)
                    {
                        exceptionMessageBuilder.AppendLine($"The {dThresholdStat.ToString().ToLower()} method coverage is below the specified {dThreshold}");
                    }

                    throw new Exception(exceptionMessageBuilder.ToString());
                }
            });

            return app.Execute(args);
        }

        static string GetAssemblyVersion() => typeof(Program).Assembly.GetName().Version.ToString();
    }
}
