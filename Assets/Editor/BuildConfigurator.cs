using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuildConfigurator
{
    [MenuItem("Azure Dreams/Configure WebGL Build")]
    public static void ConfigureWebGLBuild()
    {
        // IL2CPP para WebGL (Unity 6000 API)
        try
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            Debug.Log("[BuildConfigurator] Scripting backend set to IL2CPP.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BuildConfigurator] Could not set scripting backend: " + e.Message);
        }

        // WebGL compression
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            Debug.Log("[BuildConfigurator] WebGL compression set to Gzip.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BuildConfigurator] Could not set WebGL compression: " + e.Message);
        }

        // Memory size for WebGL
        try
        {
            PlayerSettings.WebGL.initialMemorySize = 512;
            Debug.Log("[BuildConfigurator] WebGL initial memory size set to 512 MB.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BuildConfigurator] Could not set WebGL memory size: " + e.Message);
        }

        Debug.Log("[BuildConfigurator] WebGL build configuration complete.");
    }
}
