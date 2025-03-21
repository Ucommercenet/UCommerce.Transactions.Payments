using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;

partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    [Parameter("The directory where the Ucommerce Core Nuget package can be found")]
    AbsolutePath UcommerceNugetSource
    {
        get => UcommerceNugetSourceField ?? ArtifactsDirectory;
        set => UcommerceNugetSourceField = value;
    }
    AbsolutePath UcommerceNugetSourceField;

    // ReSharper disable once UnusedMember.Local
    Target Clean => _ => _
        .Description("Cleans up bin and obj folders as well the artifacts directory.")
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(dir => dir.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Description("Restores nuget dependencies for the solution")
        .Executes(() =>
        {
            NuGetTasks.NuGetRestore(settings => settings.SetTargetPath(Solution));
        });

    // ReSharper disable once UnusedMember.Local
    Target UpdateUcommerceCore => _ => _
        .Description("Updates the Ucommerce.Core dependency to one in a local folder\n\n" +
                     "    The parameter `UcommerceNugetSource` defines the folder\n" +
                     "    Default for `UcommerceNugetSource` is the artifacts directory.")
        .After(Restore)
        .Before(Compile)
        .Executes(() =>
        {
            NuGetTasks.NuGet($"update {Solution.Path} -Id Ucommerce.Core -source {UcommerceNugetSource} -Prerelease");
        });

    Target Compile => _ => _
        .Description("Compiles the solution")
        .DependsOn(Restore)
        .Executes(() =>
        {
            MSBuild(s => s
                .SetTargetPath(Solution)
                .SetTargets("Rebuild")
                .SetConfiguration(Configuration)
                .SetMaxCpuCount(Environment.ProcessorCount)
                .SetNodeReuse(IsLocalBuild));
        });

    AbsolutePath TempWorkDir => TemporaryDirectory / "dist";
    Target GatherFiles => _ => _
        .Description("Gathers payment providers to a local folder for use by package and deploy targets")
        .DependsOn(Compile)
        .Unlisted()
        .Executes(() =>
        {
            TempWorkDir.CreateOrCleanDirectory();

            // Some projects have extra files that need to be copied to output apart from the project dll and the configs. 
            var projectExtraFilesHook = new Dictionary<string, Action<AbsolutePath, Configuration>>()
            {
                {"PayPal", (outputDirectory, configuration) =>
                {
                    var project = Solution.GetProject("Ucommerce.Transactions.Payments.PayPal");
                    (project.GetOutputDir(configuration) / "paypal_base.dll").CopyToDirectory(outputDirectory / "bin");
                }},
                {"Adyen", (outputDirectory, configuration) =>
                {
                    var project = Solution.GetProject("Ucommerce.Transactions.Payments.Adyen");
                    (project.GetOutputDir(configuration) / "Adyen.dll").CopyToDirectory(outputDirectory / "bin");
                }},
                {"Braintree", (outputDirectory, configuration) =>
                {
                    var project = Solution.GetProject("Ucommerce.Transactions.Payments.Braintree");

                    (project.GetOutputDir(configuration) / "Braintree.dll").CopyToDirectory(outputDirectory / "bin");
                    (project.GetOutputDir(configuration) / "Newtonsoft.Json.dll").CopyToDirectory(outputDirectory / "bin");
                    (project.Directory / "BraintreePaymentForm.htm").CopyToDirectory(outputDirectory);
                }},
                {
                    "QuickpayLink", (outputDirectory, configuration) =>
                    {
                        var project = Solution.GetProject("Ucommerce.Transactions.Payments.QuickpayLink");
                        (project.GetOutputDir(configuration) / "Newtonsoft.Json.dll").CopyToDirectory(outputDirectory / "bin");
                    }
                },
                {"Stripe", (outputDirectory, configuration) =>
                {
                    var project = Solution.GetProject("Ucommerce.Transactions.Payments.Stripe");

                    (project.GetOutputDir(configuration) / "Stripe.net.dll").CopyToDirectory(outputDirectory / "bin");
                    (project.GetOutputDir(configuration) / "Microsoft.Bcl.AsyncInterfaces.dll").CopyToDirectory(outputDirectory / "bin");
                    (project.Directory / "StripePaymentForm.htm").CopyToDirectory(outputDirectory);
                }}
            };

            Solution
                .GetAllProjects("Ucommerce.Transactions.Payments.*")
                .Where(project => project.Name != "Ucommerce.Transactions.Payments.Test")
                .ForEach(project =>
                {
                    var outputDir = project.GetOutputDir(Configuration);
                    var providerName = project.Name.Split(".").Last();
                    (outputDir / $"{project.Name}.dll").CopyToDirectory(TempWorkDir / providerName / "bin");
                    (project.Directory / "Configuration").Copy(TempWorkDir / providerName / "Configuration");
                    if (projectExtraFilesHook.ContainsKey(providerName))
                    {
                        projectExtraFilesHook[providerName].Invoke(TempWorkDir / providerName, Configuration);
                    }
                });
        });

    // ReSharper disable once UnusedMember.Local
    Target Package => _ => _
        .Description("Packages the payment providers to the artifacts folder as paymentProviders.zip")
        .DependsOn(GatherFiles)
        .Executes(() =>
        {
            (ArtifactsDirectory / "paymentProviders.zip").DeleteFile();
            TempWorkDir.CompressTo(ArtifactsDirectory / "paymentProviders.zip");
        });

    [Parameter] AbsolutePath DeployDirectory;

    // ReSharper disable once UnusedMember.Local
    Target DeployToLocal => _ => _
        .Description("Deploys to a local folder like a website")
        .DependsOn(GatherFiles)
        .Requires(() => DeployDirectory)
        .Executes(() =>
        {
            TempWorkDir.GlobDirectories("*").ForEach(path =>
            {
                var relativePath = TempWorkDir.GetRelativePathTo(path);
                (DeployDirectory / relativePath).CreateOrCleanDirectory();
                path.Copy(DeployDirectory / relativePath, ExistsPolicy.MergeAndOverwrite);
            });
        });
}
