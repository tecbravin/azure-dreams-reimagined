using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

public class BuildConfiguratorTests
{
    [Test]
    public void ConfigureWebGLBuild_UsesSupportedApis_AndOnlyChangesWebGL()
    {
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL);
        var standaloneBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
        var oldCompression = PlayerSettings.WebGL.compressionFormat;
        int oldMemory = PlayerSettings.WebGL.initialMemorySize;
        var oldTarget = EditorUserBuildSettings.activeBuildTarget;
        try
        {
            BuildConfigurator.ConfigureWebGLBuild();
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL),
                Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(WebGLCompressionFormat.Gzip));
            Assert.That(PlayerSettings.WebGL.initialMemorySize, Is.EqualTo(512));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone), Is.EqualTo(standaloneBackend));
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(oldTarget));
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, oldBackend);
            PlayerSettings.WebGL.compressionFormat = oldCompression;
            PlayerSettings.WebGL.initialMemorySize = oldMemory;
        }
    }
}
