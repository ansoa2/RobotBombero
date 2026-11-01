using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace RobotBombero.Tests
{
    /// <summary>Servidor con una carpeta wwwroot temporal y base de datos aislada (nunca toca Server/data).</summary>
    public class HostingTests : IDisposable
    {
        private const string FakeClientIp = "203.0.113.77";

        private readonly string dir;
        private readonly string dbPath;
        private readonly WebApplicationFactory<Program> factory;
        private readonly HttpClient client;
        private readonly CapturingLoggerProvider logs = new CapturingLoggerProvider();

        public HostingTests()
        {
            string id = Guid.NewGuid().ToString("N");
            dir = Path.Combine(Path.GetTempPath(), "rb-www-" + id);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "index.html"), "<html><body>Robot Bombero</body></html>");
            File.WriteAllBytes(Path.Combine(dir, "game.wasm"), new byte[] { 0, 0x61, 0x73, 0x6d });
            File.WriteAllBytes(Path.Combine(dir, "game.wasm.br"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(dir, "game.data.br"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(dir, "game.framework.js.br"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(dir, "game.wasm.unityweb"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(dir, "game.data"), new byte[] { 1, 2, 3 });
            File.WriteAllText(Path.Combine(dir, "game.symbols.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "game.loader.js"), "//");
            File.WriteAllBytes(Path.Combine(dir, "LICENSE"), new byte[] { 1 });

            dbPath = Path.Combine(Path.GetTempPath(), "rb-test-" + id + ".db");
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Default", "Data Source=" + dbPath + ";Pooling=False");
                b.UseWebRoot(dir);
                b.ConfigureServices(s =>
                {
                    s.AddSingleton<ILoggerProvider>(logs);
                    // Simula un cliente con IP conocida para comprobar que no llega a los logs.
                    s.AddTransient<IStartupFilter>(_ => new FakeIpStartupFilter());
                });
            });
            client = factory.CreateClient();
        }

        public void Dispose()
        {
            client.Dispose();
            factory.Dispose();
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm", dbPath + "-journal" })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch (IOException) { }
            }
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        private static StringContent PlayerJson(string name)
        {
            return new StringContent("{\"name\":\"" + name + "\"}", Encoding.UTF8, "application/json");
        }

        [Fact]
        public async Task Root_ServesIndex()
        {
            var r = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("text/html", r.Content.Headers.ContentType.MediaType);
            Assert.Contains("Robot Bombero", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Wasm_HasWasmContentType()
        {
            var r = await client.GetAsync("/game.wasm");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("application/wasm", r.Content.Headers.ContentType.MediaType);
            Assert.Empty(r.Content.Headers.ContentEncoding);
        }

        [Theory]
        [InlineData("/game.data", "application/octet-stream")]
        [InlineData("/game.symbols.json", "application/json")]
        [InlineData("/game.loader.js", "text/javascript")]
        [InlineData("/LICENSE", "application/octet-stream")]
        public async Task OtherUnityFiles_HaveContentType(string url, string type)
        {
            var r = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(type, r.Content.Headers.ContentType.MediaType);
            Assert.Empty(r.Content.Headers.ContentEncoding);
        }

        [Theory]
        [InlineData("/game.wasm.br", "application/wasm")]
        [InlineData("/game.data.br", "application/octet-stream")]
        [InlineData("/game.framework.js.br", "text/javascript")]
        public async Task BrotliFiles_KeepUnderlyingTypeAndSetEncoding(string url, string type)
        {
            var r = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(type, r.Content.Headers.ContentType.MediaType);
            Assert.Equal(new[] { "br" }, r.Content.Headers.ContentEncoding.ToArray());
        }

        [Fact]
        public async Task Unityweb_IsOctetStreamWithoutEncoding()
        {
            var r = await client.GetAsync("/game.wasm.unityweb");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("application/octet-stream", r.Content.Headers.ContentType.MediaType);
            Assert.Empty(r.Content.Headers.ContentEncoding);
        }

        [Fact]
        public async Task UnknownApiRoute_Returns404_NotIndex()
        {
            var r = await client.GetAsync("/api/nope");
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
            Assert.DoesNotContain("Robot Bombero", await r.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Api_StillAnswersUnderApi()
        {
            var r = await client.PostAsync("/api/players", PlayerJson("Ana"));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        [Fact]
        public async Task RequestLogs_DoNotContainClientIp()
        {
            await client.GetAsync("/");
            await client.GetAsync("/game.wasm");
            await client.GetAsync("/api/nope");
            await client.PostAsync("/api/players", PlayerJson("Beto"));

            var lines = logs.Lines.ToList();
            Assert.NotEmpty(lines); // el proveedor realmente está capturando
            Assert.DoesNotContain(lines, l => l.Contains(FakeClientIp));
        }

        private class FakeIpStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.Use((ctx, nxt) =>
                    {
                        ctx.Connection.RemoteIpAddress = IPAddress.Parse(FakeClientIp);
                        return nxt();
                    });
                    next(app);
                };
            }
        }

        private class CapturingLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
            public IEnumerable<string> Lines { get { return lines; } }
            public ILogger CreateLogger(string categoryName) { return new CapturingLogger(categoryName, lines); }
            public void Dispose() { }

            private class CapturingLogger : ILogger
            {
                private readonly string category;
                private readonly ConcurrentQueue<string> lines;
                public CapturingLogger(string category, ConcurrentQueue<string> lines) { this.category = category; this.lines = lines; }
                public IDisposable BeginScope<TState>(TState state) { return null; }
                public bool IsEnabled(LogLevel logLevel) { return true; }
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    string text = category + ": " + formatter(state, exception);
                    if (state is IEnumerable<KeyValuePair<string, object>> kv)
                        text += " " + string.Join(" ", kv.Select(p => p.Key + "=" + p.Value));
                    if (exception != null) text += " " + exception;
                    lines.Enqueue(text);
                }
            }
        }
    }
}
