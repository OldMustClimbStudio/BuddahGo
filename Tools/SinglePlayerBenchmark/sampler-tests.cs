// Deterministic control-flow tests of the actual template, with fake Unity APIs.
// These do not verify native recorder availability, timing, or game performance.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Unity.Profiling;
using UnityEngine;

static class SamplerTests
{
    static int passed;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidOperationException e)
        { Check(e.Message.Contains(message), e.Message); return; }
        throw new Exception("Expected rejection: " + message);
    }
    static void Tick(SoloBenchmarkFrames sampler, double dt, long? gc = 12, long? main = 1000)
    {
        Time.realtimeSinceStartupAsDouble += dt; Time.frameCount++;
        ProfilerRecorder.Emit("GC Allocated In Frame", gc);
        ProfilerRecorder.Emit("Main Thread", main);
        sampler.DrivingTick();
    }
    static void Begin(SoloBenchmarkFrames sampler, int repeat = 1)
    {
        sampler.SeedSession(repeat); sampler.DrivingStarted();
        Tick(sampler, 30); // fake clock, no load or wait
    }
    static JsonElement Finish(SoloBenchmarkFrames sampler, string folder, bool success)
    {
        Application.targetFrameRate = 500;
        sampler.Finish(success, 0, "synthetic control-flow test");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "capture.json"))).RootElement;
    }
    static string[] Cells(string folder, int row = 1) =>
        File.ReadAllLines(Path.Combine(folder, "frames.csv"))[row].Split(',');
    static void Run(string name, Action<string> test)
    {
        string folder = Path.Combine(Path.GetTempPath(), "solo-sampler-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        Time.realtimeSinceStartupAsDouble = 0; Time.frameCount = 0;
        Debug.isDebugBuild = true; Application.targetFrameRate = 144;
        Screen.width = 1920; Screen.height = 1080;
        QualitySettings.level = 1; QualitySettings.vSyncCount = 1;
        ProfilerRecorder.ResetFake();
        try { test(folder); Check(ProfilerRecorder.Live == 0, "recorder leaked"); passed++; Console.WriteLine("PASS " + name); }
        finally { Directory.Delete(folder, true); }
    }
    static void Main()
    {
        Run("three repeats keep measured 60 separate from Home 500", folder => {
            using (var sampler = new SoloBenchmarkFrames(folder))
            {
                for (int repeat = 1; repeat <= 3; repeat++)
                { Begin(sampler, repeat); Tick(sampler, 30); Tick(sampler, 30); Check(ProfilerRecorder.Live == 0, "end-of-window release"); }
                var capture = Finish(sampler, folder, true);
                Check(capture.GetProperty("complete").GetBoolean(), "complete");
                Check(capture.GetProperty("actual").GetProperty("target_fps").GetInt32() == 60, "measurement overwritten");
                Check(capture.GetProperty("post_cleanup_actual").GetProperty("target_fps").GetInt32() == 500, "Home not retained");
                Check(capture.GetProperty("gc_samples").GetInt32() == 6, "sample count");
            }
            Check(Application.targetFrameRate == 144 && Screen.width == 1920 && QualitySettings.level == 1, "restore original settings");
        });
        Run("valid zero remains zero", folder => {
            using var sampler = new SoloBenchmarkFrames(folder);
            Begin(sampler); Tick(sampler, 60, 0); Finish(sampler, folder, false);
            Check(Cells(folder)[4] == "0", "valid zero lost");
        });
        Run("stale LastValue becomes blank, later sample retained", folder => {
            using var sampler = new SoloBenchmarkFrames(folder);
            Begin(sampler); Tick(sampler, 20, 99); Tick(sampler, 20, null); Tick(sampler, 20, 7);
            var capture = Finish(sampler, folder, false);
            Check(Cells(folder, 1)[4] == "99" && Cells(folder, 2)[4] == "" && Cells(folder, 3)[4] == "7", "stale value reused");
            Check(capture.GetProperty("gc_missing_frames").GetInt32() == 1, "missing count");
            Check(capture.GetProperty("gc_reason").GetString().StartsWith("partial"), "partial availability hidden");
        });
        Run("late optional main counter is retained", folder => {
            using var sampler = new SoloBenchmarkFrames(folder);
            sampler.SeedSession(1); sampler.DrivingStarted(); Tick(sampler, 30, 12, null);
            Tick(sampler, 60, 12, 42);
            var capture = Finish(sampler, folder, false);
            Check(capture.GetProperty("main_available").GetBoolean() && Cells(folder)[5] == "42", "late sample discarded");
        });
        Run("Release preflight fails with explicit build reason", folder => {
            Debug.isDebugBuild = false;
            using var sampler = new SoloBenchmarkFrames(folder);
            Reject(() => Begin(sampler), "Development Player");
            var capture = Finish(sampler, folder, false);
            Check(!capture.GetProperty("complete").GetBoolean() && !capture.GetProperty("gc_available").GetBoolean(), "false qualification");
            Check(capture.GetProperty("gc_reason").GetString().Contains("unsupported in Release"), "build reason");
        });
        Run("missing GC preflight fails and releases independent main", folder => {
            ProfilerRecorder.FailName = "GC Allocated In Frame";
            using var sampler = new SoloBenchmarkFrames(folder);
            Reject(() => Begin(sampler), "GC preflight failed");
            Check(ProfilerRecorder.Live == 1, "independent main did not start");
            var capture = Finish(sampler, folder, false);
            Check(capture.GetProperty("gc_reason").GetString().Contains("counter start failed"), "failure reason lost");
        });
        Run("interior settings drift aborts even before end", folder => {
            using var sampler = new SoloBenchmarkFrames(folder);
            Begin(sampler); Application.targetFrameRate = 500;
            Reject(() => Tick(sampler, 1), "settings differ");
            var capture = Finish(sampler, folder, false);
            Check(!capture.GetProperty("complete").GetBoolean(), "drift accepted");
        });
        Run("abort and repeated Dispose release all recorders", folder => {
            var sampler = new SoloBenchmarkFrames(folder);
            sampler.SeedSession(1); sampler.DrivingStarted();
            Check(ProfilerRecorder.Live == 2, "recorders not started");
            sampler.Dispose(); sampler.Dispose();
            Check(Application.targetFrameRate == 144, "restore after abort");
        });
        Console.WriteLine(passed + " sampler control-flow tests passed; native Unity runtime NOT tested");
    }
}

namespace Unity.Profiling
{
    [Flags] public enum ProfilerRecorderOptions { StartImmediately=1, SumAllSamplesInFrame=2, WrapAroundWhenCapacityReached=4 }
    public struct ProfilerCategory { public static ProfilerCategory Memory, Internal; }
    public struct ProfilerRecorder : IDisposable
    {
        sealed class State { public int count, capacity; public long value; public bool disposed; }
        static readonly Dictionary<string, State> states = new();
        State state;
        public static string FailName;
        public static int Live;
        public bool Valid => state != null && !state.disposed;
        public bool IsRunning => Valid;
        public bool WrappedAround => false;
        public int Count => state.count;
        public long LastValue => state.value;
        public static ProfilerRecorder StartNew(ProfilerCategory category, string name, int capacity, ProfilerRecorderOptions options)
        {
            if (FailName == name) throw new InvalidOperationException("synthetic start failure");
            if ((options & ProfilerRecorderOptions.WrapAroundWhenCapacityReached) != 0) throw new Exception("must not wrap");
            var state = new State { capacity=capacity }; states[name] = state; Live++;
            return new ProfilerRecorder { state=state };
        }
        public static void Emit(string name, long? value)
        {
            if (value.HasValue && states.TryGetValue(name, out var state) && !state.disposed && state.count < state.capacity)
            { state.count++; state.value=value.Value; }
        }
        public void Dispose() { if (Valid) { state.disposed=true; Live--; } }
        public static void ResetFake() { states.Clear(); FailName=null; Live=0; }
    }
}
namespace UnityEngine
{
    public enum FullScreenMode { Windowed }
    public static class QualitySettings
    {
        public static int level, vSyncCount; public static string[] names = { "low", "medium", "high" };
        public static int GetQualityLevel() => level;
        public static void SetQualityLevel(int value, bool apply) { level=value; }
    }
    public static class Application { public static int targetFrameRate; public static string unityVersion="synthetic", version="test"; }
    public static class Debug { public static bool isDebugBuild; }
    public static class Screen
    {
        public static int width, height; public static FullScreenMode fullScreenMode;
        public static void SetResolution(int w, int h, FullScreenMode mode) { width=w; height=h; fullScreenMode=mode; }
    }
    public static class Random { public struct State {} public static State state; public static void InitState(int seed) {} }
    public static class SystemInfo
    {
        public static string operatingSystem="synthetic", processorType="synthetic", graphicsDeviceName="synthetic", graphicsDeviceType="synthetic";
        public static int systemMemorySize=1;
    }
    public static class Time { public static double realtimeSinceStartupAsDouble; public static int frameCount; public static float timeScale=1; }
    public static class JsonUtility
    {
        public static string ToJson(object value, bool pretty) => JsonSerializer.Serialize(value,
            new JsonSerializerOptions { IncludeFields=true, WriteIndented=pretty });
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }
    public static class SceneManager { public static Scene GetActiveScene() => new Scene { name="RaceMap" }; }
}
