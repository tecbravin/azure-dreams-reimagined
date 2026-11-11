using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    [InitializeOnLoad]
    public static class PsxShaderTestRunner
    {
        private static readonly string Marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/PsxShaderTests.request").Replace("\\", "/");
        private const string OutputDir = "Logs/PsxShaders";

        static PsxShaderTestRunner()
        {
            Debug.Log("[PsxShaderTestRunner] Static constructor ran. Marker exists: " + File.Exists(Marker));
        }

        [MenuItem("Azure Dreams/Psx Shaders/Run Shader Tests")]
        public static void RunPsxShaderTestsMenuItem()
        {
            RunPsxShaderTests();
        }

        public static void RunPsxShaderTests()
        {
            string projectDir = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectDir, OutputDir);
            Directory.CreateDirectory(outDir);

            string xmlPath = Path.Combine(outDir, "psx-tests.xml").Replace("\\", "/");
            string jsonPath = Path.Combine(outDir, "psx-tests.json").Replace("\\", "/");

            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "AzureDreams.Reimagined.EditorTests" }
            };

            var callbacks = new ResultCallbacks(xmlPath, jsonPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(callbacks);
            Debug.Log("[PsxShaderTestRunner] Executing tests via Execute...");
            api.Execute(new ExecutionSettings(filter));
            Debug.Log("[PsxShaderTestRunner] Execute call returned.");
        }

        private class ResultCallbacks : ICallbacks
        {
            private readonly string _xmlPath;
            private readonly string _jsonPath;
            private readonly List<ITestResultAdaptor> _results = new();
            private int _passed, _failed, _skipped;

            public ResultCallbacks(string xmlPath, string jsonPath)
            {
                _xmlPath = xmlPath;
                _jsonPath = jsonPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log("[PsxShaderTestRunner] Test run started.");
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test != null && result.Message != null && result.Message.Contains("FAIL"))
                    Debug.LogError("[PsxShaderTestRunner] FAIL: " + result.Test.FullName + " -> " + result.Message);
                _results.Add(result);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    // Save XML using official API
                    TestRunnerApi.SaveResultToFile(result, _xmlPath);

                    // Build JSON from collected per-test results
                    var run = new TestRunResult();
                    foreach (var r in _results)
                    {
                        string state = r.ResultState ?? "Unknown";
                        if (state == "Passed") _passed++;
                        else if (state == "Failed") _failed++;
                        else if (state == "Skipped") _skipped++;
                        else _skipped++;

                        run.test_run.Add(new TestCaseResult
                        {
                            name = r.Test != null ? r.Test.FullName : "",
                            result = state,
                            duration_ms = (int)(r.Duration * 1000)
                        });
                    }
                    string json = JsonUtility.ToJson(run, true);
                    File.WriteAllText(_jsonPath, json, Encoding.UTF8);

                    Debug.Log("[PsxShaderTestRunner] Finished. Passed=" + _passed +
                        " Failed=" + _failed + " Skipped=" + _skipped + " Total=" + _results.Count);
                    Debug.Log("[PsxShaderTestRunner] Results: " + _xmlPath + " | " + _jsonPath);
                }
                catch (Exception e)
                {
                    Debug.LogError("[PsxShaderTestRunner] Save failed: " + e);
                }
                finally
                {
                }
            }
        }

        [Serializable]
        private class TestRunResult
        {
            public List<TestCaseResult> test_run = new();
        }

        [Serializable]
        private class TestCaseResult
        {
            public string name;
            public string result;
            public int duration_ms;
        }
    }
}