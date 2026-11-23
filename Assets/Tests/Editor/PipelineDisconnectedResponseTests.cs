using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AzureDreamsReimagined.Tests
{
    public class PipelineDisconnectedResponseTests
    {
        private const int TimeoutMilliseconds = 10000;

        [UnityTest]
        public System.Collections.IEnumerator ClosedResponseDoesNotFaultAndOpenResponseReturnsPayload()
        {
            MethodInfo helper = FindResponseHelper();
            Assert.That(helper, Is.Not.Null, "Could not find BasePipelineServer.SendCommandCatalogResponse.");

            HttpListener closedListener = null;
            HttpWebRequest closedRequest = null;
            Task<WebResponse> abandonedClientTask = null;
            try
            {
                closedListener = StartListener(out string closedUrl);
                Task<HttpListenerContext> contextTask = closedListener.GetContextAsync();
                closedRequest = CreateRequest(closedUrl);
                abandonedClientTask = closedRequest.GetResponseAsync();
                float deadline = UnityEngine.Time.realtimeSinceStartup + TimeoutMilliseconds / 1000f;
                while (!contextTask.IsCompleted && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(contextTask.IsCompleted, Is.True, "Timed out waiting for disconnected request.");
                HttpListenerContext context = contextTask.GetAwaiter().GetResult();
                context.Response.Close();
                Task send = InvokeHelper(helper, context.Response, "disconnected");
                while (!send.IsCompleted && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(send.IsCompleted, Is.True, "Disconnected response timed out.");
                send.GetAwaiter().GetResult();
                if (abandonedClientTask.IsCompleted) ObserveClientTask(abandonedClientTask);
            }
            finally
            {
                closedRequest?.Abort();
                closedListener?.Close();
            }

            HttpListener openListener = null;
            HttpWebRequest openRequest = null;
            try
            {
                openListener = StartListener(out string openUrl);
                Task<HttpListenerContext> contextTask = openListener.GetContextAsync();
                openRequest = CreateRequest(openUrl);
                Task<WebResponse> clientTask = openRequest.GetResponseAsync();
                float deadline = UnityEngine.Time.realtimeSinceStartup + TimeoutMilliseconds / 1000f;
                while (!contextTask.IsCompleted && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(contextTask.IsCompleted, Is.True, "Timed out waiting for normal request.");
                HttpListenerContext context = contextTask.GetAwaiter().GetResult();
                Task send = InvokeHelper(helper, context.Response, "payload-200");
                while (!send.IsCompleted && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(send.IsCompleted, Is.True, "Normal send timed out.");
                send.GetAwaiter().GetResult();
                deadline = UnityEngine.Time.realtimeSinceStartup + TimeoutMilliseconds / 1000f;
                while (!clientTask.IsCompleted && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(clientTask.IsCompleted, Is.True, "Timed out waiting for normal response.");
                using (WebResponse response = clientTask.GetAwaiter().GetResult())
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    Assert.That((int)((HttpWebResponse)response).StatusCode, Is.EqualTo(200));
                    Assert.That(reader.ReadToEnd(), Is.EqualTo("\"payload-200\""));
                }
            }
            finally
            {
                openRequest?.Abort();
                openListener?.Close();
            }
        }

        private static HttpListener StartListener(out string url)
        {
            int port;
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                reservation.Start();
                port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            }
            finally { reservation.Stop(); }
            url = "http://127.0.0.1:" + port + "/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return listener;
        }

        private static HttpWebRequest CreateRequest(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = TimeoutMilliseconds;
            request.ReadWriteTimeout = TimeoutMilliseconds;
            return request;
        }

        private static MethodInfo FindResponseHelper()
        {
            Type serverType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Unity.Pipeline.BasePipelineServer", false))
                .FirstOrDefault(type => type != null);
            return serverType?.GetMethod("SendCommandCatalogResponse", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(HttpListenerResponse), typeof(object) }, null);
        }

        private static Task InvokeHelper(MethodInfo helper, HttpListenerResponse response, object payload) =>
            (Task)helper.Invoke(null, new[] { response, payload });

        private static void ObserveClientTask(Task<WebResponse> task)
        {
            try { task.GetAwaiter().GetResult()?.Close(); }
            catch (WebException) { /* Expected after the response is closed. */ }
        }
    }
}
