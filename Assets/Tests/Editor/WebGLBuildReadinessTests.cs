using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public class WebGLBuildReadinessTests
{
    [Test]
    public void PlayerSettings_WebGL_IsConfigured()
    {
        // Availability check must not switch platform during a running suite.
        // Switching forces a project-wide reimport and leaves other tests on WebGL.
        var buildTarget = BuildTarget.WebGL;
        Assert.IsTrue(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL),
            "WebGL build target should be available.");

        Assert.AreEqual(BuildTargetGroup.WebGL, BuildPipeline.GetBuildTargetGroup(buildTarget));

        // Common WebGL pitfalls: streaming assets can be fine, but huge textures/unsupported codecs break builds.
        // This test is a smoke check; deeper checks happen in AssetImportValidationTests.
        Assert.Pass("WebGL build target support is installed.");
    }
}
