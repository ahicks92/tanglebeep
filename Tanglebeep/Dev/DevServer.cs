using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Tanglebeep.Speech;
using Tanglebeep.Util;

namespace Tanglebeep.Dev {
    /// <summary>
    /// In-process dev driver, on by default (set TANGLEBEEP_NO_DEV=1 to disable). The HTTP
    /// server binds 127.0.0.1 only (see <see cref="DevHttpServer"/>), so it is reachable from
    /// this machine alone. Exposes that loopback HTTP server so an external driver can:
    ///   POST /eval         body = C# source, run against the live game (REPL state
    ///                      persists across calls); returns output + result/errors.
    ///   POST /input        body = verb. Drives the GAME's own UIObject focus / hero turns.
    ///   POST /menu         body = verb. Drives the MOD's overlay cursor (the InputQueue path),
    ///                      the only way to test an overlay that captures input.
    ///   POST /loadsave     body = save slot index (default 0). Loads that slot from the
    ///                      title screen and BLOCKS until the gameplay scene is interactive.
    ///   GET  /speech?since=N   lines the mod has spoken since cursor N (we can't hear
    ///                          the TTS, so this is how we observe it).
    ///   GET  /health       liveness.
    ///
    /// Eval runs on the Unity main thread: HTTP requests enqueue a job and block until
    /// <see cref="Pump"/> (called from Plugin.Update) executes it. /speech reads a
    /// thread-safe buffer directly off the HTTP thread. Not shipped to players.
    /// </summary>
    public sealed class DevServer {
        public const string DisableEnv = "TANGLEBEEP_NO_DEV";
        public const string PortEnv = "TANGLEDEEP_DEV_PORT";
        private const int DefaultPort = 8770;

        private sealed class Job {
            public Func<string> Work;
            public string Result = "";
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private readonly SpeechLog _speech = new SpeechLog();
        private readonly CSharpEvaluator _evaluator = new CSharpEvaluator();
        private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private DevHttpServer _http;
        private bool _enabled;
        private bool _runInBackgroundForced;

        /// <summary>Stand up the loopback server unless TANGLEBEEP_NO_DEV=1.</summary>
        public void Start() {
            if (Environment.GetEnvironmentVariable(DisableEnv) == "1") {
                Log.Info("Dev server disabled (TANGLEBEEP_NO_DEV=1)");
                return;
            }

            int port = DefaultPort;
            string p = Environment.GetEnvironmentVariable(PortEnv);
            if (!string.IsNullOrEmpty(p)) {
                int.TryParse(p, out port);
            }

            // Tap every string the mod speaks (single chokepoint) into the ring buffer.
            PrismSpeech.Observer = _speech.Add;

            try {
                _http = new DevHttpServer(port, HandleRequest);
                _http.Start();
                _enabled = true;
                Log.Info("Dev server on http://127.0.0.1:" + port + " (POST /eval, GET /speech)");
            } catch (Exception e) {
                Log.Error("Dev server failed to start: " + e);
            }
        }

        /// <summary>Run queued main-thread jobs. Call once per frame from Update.</summary>
        public void Pump() {
            if (!_enabled) {
                return;
            }
            if (!_runInBackgroundForced) {
                // Insurance: keep the game simulating while unfocused, which is how we drive it.
                // Tangledeep already ships this true; guard against a future patch flipping it.
                UnityEngine.Application.runInBackground = true;
                _runInBackgroundForced = true;
            }
            Job job;
            while (_jobs.TryDequeue(out job)) {
                try {
                    job.Result = job.Work() ?? "";
                } catch (Exception e) {
                    job.Result = "[host error] " + e + "\n";
                }
                job.Done.Set();
            }
        }

        /// <summary>Run <paramref name="work"/> on the main thread (next Pump) and block for its result.</summary>
        private string OnMainThread(Func<string> work, int timeoutSeconds = 30) {
            var job = new Job { Work = work };
            _jobs.Enqueue(job);
            if (!job.Done.Wait(TimeSpan.FromSeconds(timeoutSeconds))) {
                return "[timeout] main thread did not run the job within " + timeoutSeconds + "s (frozen / not pumping?)\n";
            }
            return job.Result;
        }

        // Runs on the HTTP thread.
        private string HandleRequest(string method, string path, string body) {
            string route = path;
            string query = "";
            int q = path.IndexOf('?');
            if (q >= 0) {
                route = path.Substring(0, q);
                query = path.Substring(q + 1);
            }

            if (route == "/eval" && method == "POST") {
                if (string.IsNullOrWhiteSpace(body)) {
                    return "[empty] POST C# source as the request body\n";
                }
                return OnMainThread(() => _evaluator.Eval(body));
            }

            if (route == "/gui/game" && method == "GET") {
                return OnMainThread(() => GuiInspector.DumpGameUi());
            }

            if (route == "/gui/mod" && method == "GET") {
                return OnMainThread(() => GuiInspector.DumpModUi());
            }

            if (route == "/input" && method == "POST") {
                string verb = (body ?? "").Trim();
                return OnMainThread(() => InputInjector.Inject(verb));
            }

            if (route == "/menu" && method == "POST") {
                string verb = (body ?? "").Trim();
                return OnMainThread(() => MenuInjector.Inject(verb));
            }

            if (route == "/loadsave" && method == "POST") {
                return LoadSave(body);
            }

            if (route == "/screenshot" && method == "GET") {
                return Screenshot();
            }

            if (route == "/speech" && method == "GET") {
                long since = 0;
                foreach (string kv in query.Split('&')) {
                    if (kv.StartsWith("since=", StringComparison.Ordinal)) {
                        long.TryParse(kv.Substring("since=".Length), out since);
                    }
                }
                long next;
                string lines = _speech.Render(since, out next);
                return "cursor: " + next + "\n" + lines;
            }

            if (route == "/health" || route == "/") {
                return "ok\n";
            }

            return "[404] " + method + " " + route + "\n";
        }

        // Trigger a screenshot on the main thread, then wait (on this HTTP thread) for the PNG,
        // which ScreenCapture writes asynchronously over the next frame(s). Returns the path,
        // which the driver then reads to view the frame.
        private string Screenshot() {
            string path = Path.Combine(Path.GetTempPath(), "td_shot.png");
            OnMainThread(() => {
                try {
                    if (File.Exists(path)) {
                        File.Delete(path);
                    }
                } catch {
                }
                UnityEngine.ScreenCapture.CaptureScreenshot(path);
                return "requested";
            });

            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < 8) {
                try {
                    if (File.Exists(path)) {
                        long size = new FileInfo(path).Length;
                        if (size > 0) {
                            Thread.Sleep(60); // let the write settle, then confirm size is stable
                            if (new FileInfo(path).Length == size) {
                                return path + "\n";
                            }
                        }
                    }
                } catch {
                }
                Thread.Sleep(50);
            }
            return "[timeout] screenshot not written within 8s\n";
        }

        // Load a save slot directly from the title screen and BLOCK until the gameplay scene
        // is fully interactive, so the driver can script "drop me into slot N" in one call.
        //
        // Drives the same path the CONTINUE button uses: set the slot, mark it a load (not a
        // new game), stash the LOADGAME response the gameplay-scene init reads, and start the
        // FadeOutThenLoadGame coroutine. We kick it on the main thread, then poll (from this
        // HTTP thread) for gameLoadSequenceCompleted, which the load coroutine sets true on its
        // final line. We force it false first so a second in-session load can't observe a stale
        // true and return early.
        //
        // The load's GameMasterScript init force-sets tdHasFocus=true regardless of real OS
        // focus (GameMasterScript.cs ~2378), and TDInputHandler gates physical-key processing on
        // that flag - so without correction the game would eat stray keystrokes while it runs
        // unfocused in the background. Once the load settles we set tdHasFocus to whether the
        // game window is *actually* the OS foreground window. We can't trust
        // UnityEngine.Application.isFocused here: it initializes true and only flips on an
        // OnApplicationFocus(false) event, so a game launched in the background (never focused,
        // so never a focus-loss event) reports true forever - the same lie. We ask Win32 instead.
        private string LoadSave(string body) {
            int slot = 0;
            string trimmed = (body ?? "").Trim();
            if (trimmed.Length > 0 && !int.TryParse(trimmed, out slot)) {
                return "[bad slot] body must be an integer save slot index (default 0)\n";
            }

            string kick = OnMainThread(() => {
                UIManagerScript ums = UIManagerScript.singletonUIMS;
                if (ums == null) {
                    return "[no UIManagerScript] not on a screen that can start a load\n";
                }
                // The dev server answers /health a moment before the title screen finishes
                // initializing. TitleScreenStart is what flips bReadyForMainMenuDialog and, in the
                // same pass, allocates UIManagerScript.allUIObjects. Kicking the load before that
                // runs leaves allUIObjects null, and the gameplay-scene dialog cursor alignment
                // (AlignCursorPos -> SetCursorAsChildOfDialogBox) then throws on a null collection
                // the first time any dialog opens. Wait for the main menu to be ready (the caller
                // retries on a non-"loaded" response, so this just defers the kick).
                if (!TitleScreenScript.bReadyForMainMenuDialog || UIManagerScript.allUIObjects == null) {
                    return "[not ready] title screen still initializing; retry\n";
                }
                GameStartData.saveGameSlot = slot;
                GameStartData.newGame = false;
                GameMasterScript.gameLoadSequenceCompleted = false;
                UIManagerScript.SetGlobalResponse(DialogButtonResponse.LOADGAME);
                ums.StartCoroutine(ums.FadeOutThenLoadGame());
                return "ok";
            });
            if (kick != "ok") {
                return kick;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < 60) {
                string status = OnMainThread(() => {
                    if (!GameMasterScript.gameLoadSequenceCompleted
                        || GameMasterScript.heroPCActor == null
                        || GameMasterScript.gmsSingleton == null) {
                        return "";
                    }
                    bool focused = GameWindowIsForeground();
                    GameMasterScript.gmsSingleton.tdHasFocus = focused;
                    string map = MapMasterScript.activeMap != null ? MapMasterScript.activeMap.GetName() : "?";
                    return "loaded slot " + slot + ": hero=" + GameMasterScript.heroPCActor.displayName
                        + " map=" + map + " focus=" + focused + "\n";
                });
                if (status.Length > 0) {
                    return status;
                }
                Thread.Sleep(100);
            }
            return "[timeout] load slot " + slot + " did not complete within 60s\n";
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // True only if our process owns the actual OS foreground window. Unlike
        // Application.isFocused this reflects real focus even for a never-focused background launch.
        private static bool GameWindowIsForeground() {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) {
                return false;
            }
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            return pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }
    }
}
