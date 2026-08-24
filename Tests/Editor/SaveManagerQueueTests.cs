// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Buck.SaveAsync;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// EditMode regression tests for the SaveManager operation queue: single-drain ownership,
/// completion semantics of awaited requests, no stranded requests, slot capture at enqueue
/// time, and failure propagation. File I/O is replaced with an in-memory stub handler whose
/// writes can be gated on a TaskCompletionSource, so overlap windows are deterministic.
/// </summary>
public class SaveManagerQueueTests
{
    class StubFileHandler : FileHandler
    {
        public readonly List<(string path, int slotIndex)> Writes = new();
        public readonly Dictionary<string, string> Files = new();
        public TaskCompletionSource<bool> WriteGate; // one-shot: the next write awaits it
        public Exception ThrowOnWrite;               // one-shot: the next write throws it
        public int WritesStarted;
        public int MaxConcurrentWrites;
        int m_concurrentWrites;

        public override bool Exists(string pathOrFilename) => Files.ContainsKey(pathOrFilename);

        public override async Task WriteFile(string pathOrFilename, string content, CancellationToken cancellationToken)
        {
            WritesStarted++;
            m_concurrentWrites++;
            MaxConcurrentWrites = Math.Max(MaxConcurrentWrites, m_concurrentWrites);
            try
            {
                // Record the slot index the way a real handler observes it while resolving paths.
                Writes.Add((pathOrFilename, SaveManager.SaveSlotIndex));

                var gate = WriteGate;
                if (gate != null)
                {
                    WriteGate = null;
                    await gate.Task;
                }

                if (ThrowOnWrite != null)
                {
                    var e = ThrowOnWrite;
                    ThrowOnWrite = null;
                    throw e;
                }

                Files[pathOrFilename] = content;
            }
            finally
            {
                m_concurrentWrites--;
            }
        }

        public override Task<string> ReadFile(string pathOrFilename, CancellationToken cancellationToken)
            => Task.FromResult(Files.TryGetValue(pathOrFilename, out var content) ? content : string.Empty);

        public override Task Delete(string pathOrFilename, CancellationToken cancellationToken)
        {
            Files.Remove(pathOrFilename);
            return Task.CompletedTask;
        }
    }

    class TestSaveable : ISaveable<string>
    {
        public string Key { get; set; } = "TestSaveable";
        public string Filename { get; set; } = "TestFile";
        public StorageScope Scope { get; set; } = StorageScope.Global;
        public int Version => 0;
        public Action OnRestore;
        public string CaptureState() => "state";
        public void RestoreState(string state) => OnRestore?.Invoke();
    }

    const BindingFlags k_staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
    static readonly Type k_saveManager = typeof(SaveManager);
    static readonly Type k_singleton = typeof(Singleton<SaveManager>);

    GameObject m_gameObject;
    StubFileHandler m_handler;

    static void SetStatic(string field, object value)
        => k_saveManager.GetField(field, k_staticFlags).SetValue(null, value);

    static object GetStatic(string field)
        => k_saveManager.GetField(field, k_staticFlags).GetValue(null);

    static void SetIsBusy(bool value)
        => k_saveManager.GetProperty(nameof(SaveManager.IsBusy)).SetValue(null, value);

    static int QueueCount
        => ((System.Collections.ICollection)GetStatic("m_fileOperationQueue")).Count;

    static void ClearStaticCollections()
    {
        ((System.Collections.IDictionary)GetStatic("m_saveables")).Clear();
        ((System.Collections.IDictionary)GetStatic("s_fileScopes")).Clear();
        ((System.Collections.IList)GetStatic("m_loadedSaveables")).Clear();
        var queue = GetStatic("m_fileOperationQueue");
        queue.GetType().GetMethod("Clear").Invoke(queue, null);
    }

    static void ClearSlotOverride()
        => ((AsyncLocal<int?>)GetStatic("s_slotIndexOverride")).Value = null;

    [SetUp]
    public void SetUp()
    {
        // Pre-create the singleton instance so the Instance getter finds it instead of
        // auto-creating one (auto-create calls DontDestroyOnLoad, which throws in edit mode).
        m_gameObject = new GameObject("SaveManagerQueueTests");
        var instance = m_gameObject.AddComponent<SaveManager>();
        k_singleton.GetField("m_Instance", k_staticFlags).SetValue(null, instance);
        k_singleton.GetField("m_ShuttingDown", k_staticFlags).SetValue(null, false);

        m_handler = ScriptableObject.CreateInstance<StubFileHandler>();
        SetStatic("m_fileHandler", m_handler);
        SetStatic("m_initialized", true);

        ClearStaticCollections();
        ClearSlotOverride();
        SetIsBusy(false);
        SaveManager.SaveSlotIndex = -1;
    }

    [TearDown]
    public void TearDown()
    {
        if (m_gameObject != null)
            UnityEngine.Object.DestroyImmediate(m_gameObject);
        if (m_handler != null)
            UnityEngine.Object.DestroyImmediate(m_handler);

        // DestroyImmediate runs OnDestroy, which flips the singleton's shutting-down flag.
        k_singleton.GetField("m_Instance", k_staticFlags).SetValue(null, null);
        k_singleton.GetField("m_ShuttingDown", k_staticFlags).SetValue(null, false);

        ((CancellationTokenSource)GetStatic("s_linkedLifetimeCts"))?.Dispose();
        SetStatic("s_linkedLifetimeCts", null);
        SetStatic("s_linkedLifetimeOwner", null);

        SetStatic("m_fileHandler", null);
        SetStatic("m_initialized", false);
        ClearStaticCollections();
        ClearSlotOverride();
        SetIsBusy(false);
        SaveManager.SaveSlotIndex = -1;
    }

    static TaskCompletionSource<bool> NewGate()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Test]
    public async Task BlockedCallsDoNotReleaseBusyAndOperationsNeverOverlap()
    {
        SaveManager.RegisterSaveable(new TestSaveable());
        var gate = NewGate();
        m_handler.WriteGate = gate;

        var op1 = SaveManager.Save("TestFile"); // claims the queue, parks inside WriteFile
        Assert.IsTrue(SaveManager.IsBusy);
        Assert.AreEqual(1, m_handler.WritesStarted);

        var op2 = SaveManager.Save("TestFile"); // queued behind op1
        Assert.IsTrue(SaveManager.IsBusy, "a blocked call must not release IsBusy");

        var op3 = SaveManager.Save("TestFile"); // pre-0.14.2 this started a second, concurrent drain
        Assert.AreEqual(1, m_handler.WritesStarted, "no second drain may start while the first is in flight");

        gate.SetResult(true);
        await op1;
        await op2;
        await op3;

        Assert.AreEqual(3, m_handler.WritesStarted, "every request must eventually execute");
        Assert.AreEqual(1, m_handler.MaxConcurrentWrites, "operations must never overlap");
        Assert.IsFalse(SaveManager.IsBusy);
        Assert.AreEqual(0, QueueCount);
    }

    [Test]
    public async Task AwaitingAQueuedRequestCompletesOnlyAfterItExecutes()
    {
        SaveManager.RegisterSaveable(new TestSaveable());
        var gate = NewGate();
        m_handler.WriteGate = gate;

        var op1 = SaveManager.Save("TestFile");
        var op2 = SaveManager.Save("TestFile");

        var awaiter2 = op2.GetAwaiter();
        Assert.IsFalse(awaiter2.IsCompleted,
            "awaiting a queued request must not complete before the request has executed");

        gate.SetResult(true);
        await op1; // the owning drain finishes every queued request before returning

        Assert.IsTrue(awaiter2.IsCompleted, "the queued request completes once it has executed");
        Assert.AreEqual(2, m_handler.WritesStarted);
    }

    [Test]
    public async Task RequestsEnqueuedDuringRestoreAreExecutedNotStranded()
    {
        bool fired = false;
        var saveable = new TestSaveable();
        saveable.OnRestore = () =>
        {
            if (fired)
                return;
            fired = true;
            _ = SaveManager.Save("TestFile"); // fire-and-forget from inside RestoreState
        };
        SaveManager.RegisterSaveable(saveable);

        LogAssert.Expect(LogType.Warning, new Regex("was not restored from save data"));
        await SaveManager.Load("TestFile"); // file missing -> defaults -> OnRestore fires mid-drain

        Assert.IsTrue(fired);
        Assert.AreEqual(1, m_handler.WritesStarted, "the nested save must actually execute");
        Assert.AreEqual(0, QueueCount, "the nested request must not be left behind in the queue");
        Assert.IsFalse(SaveManager.IsBusy);
    }

    [Test]
    public async Task QueuedRequestsExecuteWithTheSlotIndexTheyWereRequestedUnder()
    {
        SaveManager.RegisterSaveable(new TestSaveable { Scope = StorageScope.Slot });
        var gate = NewGate();
        m_handler.WriteGate = gate;

        SaveManager.SaveSlotIndex = 0;
        var op1 = SaveManager.Save("TestFile"); // executes under slot 0, parked at the gate

        SaveManager.SaveSlotIndex = 1;
        var op2 = SaveManager.Save("TestFile"); // requested under slot 1, queued

        SaveManager.SaveSlotIndex = 2; // the game moves on before the queue drains

        gate.SetResult(true);
        await op1;
        await op2;

        Assert.AreEqual(2, m_handler.Writes.Count);
        Assert.AreEqual(0, m_handler.Writes[0].slotIndex);
        Assert.AreEqual(1, m_handler.Writes[1].slotIndex,
            "a queued request must execute under the slot it was requested under, not the current slot");
        Assert.AreEqual(2, SaveManager.SaveSlotIndex, "the game-facing slot index must be unaffected");
    }

    [Test]
    public async Task FailedDrainFaultsQueuedRequestsAndReleasesTheQueue()
    {
        SaveManager.RegisterSaveable(new TestSaveable());
        var gate = NewGate();
        m_handler.WriteGate = gate;
        m_handler.ThrowOnWrite = new InvalidOperationException("disk full");

        LogAssert.Expect(LogType.Error, new Regex("SaveFileOperationAsync"));
        LogAssert.Expect(LogType.Error, new Regex("DoFileOperation"));

        var op1 = SaveManager.Save("TestFile"); // throws once the gate opens
        var op2 = SaveManager.Save("TestFile"); // queued; must observe the failure too

        gate.SetResult(true);

        bool op1Threw = false;
        bool op2Threw = false;
        try { await op1; } catch (InvalidOperationException) { op1Threw = true; }
        try { await op2; } catch (InvalidOperationException) { op2Threw = true; }

        Assert.IsTrue(op1Threw, "the owning call must observe the exception");
        Assert.IsTrue(op2Threw, "queued requests must observe the exception instead of waiting forever");
        Assert.IsFalse(SaveManager.IsBusy, "a failed drain must release the queue");
        Assert.AreEqual(0, QueueCount);

        await SaveManager.Save("TestFile"); // and the system recovers cleanly
        Assert.IsFalse(SaveManager.IsBusy);
        Assert.IsTrue(m_handler.Files.ContainsKey("TestFile"));
    }
}
