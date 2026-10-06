// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Buck.SaveAsync;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Checks that SaveManager and Singleton&lt;T&gt; start every Play session from a clean slate when the
/// editor does not reload the scripting domain (Enter Play Mode Settings). The test runs two Play
/// sessions with domain reload disabled: the first uses the manager the way a game would and ends
/// with a request still queued behind a write that never finishes, and the second must find the
/// singleton available, the manager uninitialized and empty, and the abandoned request released.
/// The project's Enter Play Mode Settings are restored afterwards. With the domain reload on
/// there is nothing to test: the reload itself resets it all.
/// </summary>
public class SaveManagerPlayModeTests
{
    class InMemoryFileHandler : FileHandler
    {
        public readonly Dictionary<string, string> Files = new();
        public TaskCompletionSource<bool> WriteGate; // one-shot: the next write awaits it

        public override bool Exists(string pathOrFilename) => Files.ContainsKey(pathOrFilename);

        public override async Task WriteFile(string pathOrFilename, string content, CancellationToken cancellationToken)
        {
            var gate = WriteGate;
            if (gate != null)
            {
                WriteGate = null;
                await gate.Task;
            }

            Files[pathOrFilename] = content;
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
        public string Key => "PlayModeStaticsTests";
        public string Filename => k_filename;
        public StorageScope Scope => StorageScope.Global;
        public int Version => 0;
        public string CaptureState() => "state";
        public void RestoreState(string state) { }
    }

    const string k_filename = "PlayModeStaticsTests";
    const BindingFlags k_staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
    static readonly Type k_saveManager = typeof(SaveManager);
    static readonly Type k_singleton = typeof(Singleton<SaveManager>);

    bool m_optionsEnabled;
    EnterPlayModeOptions m_options;

    // Failures are collected rather than thrown so that the editor always leaves Play mode at the
    // end of the test, whatever went wrong inside a session.
    readonly List<string> m_failures = new();

    [SetUp]
    public void SetUp()
    {
        m_optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        m_options = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        m_failures.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        EditorSettings.enterPlayModeOptions = m_options;
        EditorSettings.enterPlayModeOptionsEnabled = m_optionsEnabled;
    }

    [UnityTest]
    public IEnumerator StateIsResetAtTheStartOfEveryPlaySessionWithoutDomainReload()
    {
        // Session one: use the manager the way a game would, then leave a request queued behind a
        // write that never finishes, as a session that ends in the middle of a save would.
        yield return new EnterPlayMode(expectDomainReload: false);

        Check(SaveManager.Instance != null, "session 1: SaveManager.Instance is created");
        SaveManager.RegisterSaveable(new TestSaveable());
        Check(SaveableCount == 1, "session 1: the saveable is registered");
        Check((bool)GetStatic(k_saveManager, "m_initialized"), "session 1: the manager initialized itself");
        SaveManager.SaveSlotIndex = 2;

        var handler = ScriptableObject.CreateInstance<InMemoryFileHandler>();
        handler.WriteGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetStatic(k_saveManager, "m_fileHandler", handler);
        _ = SaveManager.Save(k_filename);          // claims the queue and parks inside WriteFile
        var queued = SaveManager.Save(k_filename); // queued behind it, never executed
        Check(SaveManager.IsBusy, "session 1: a save is in flight");
        Check(QueueCount == 1, "session 1: a second save is queued");

        yield return new ExitPlayMode();

        // Between sessions: without a domain reload all of it is still there.
        Check((bool)GetStatic(k_singleton, "m_ShuttingDown"), "between sessions: Singleton<T> is still flagged as shutting down");
        Check((bool)GetStatic(k_saveManager, "m_initialized"), "between sessions: the manager is still initialized");
        Check(SaveableCount == 1, "between sessions: the saveable is still registered");
        Check(SaveManager.SaveSlotIndex == 2, "between sessions: the slot index is kept");
        Check(SaveManager.IsBusy && QueueCount == 1, "between sessions: the queue is still busy with the abandoned request");

        // Session two: ResetForPlaySession ran at SubsystemRegistration.
        yield return new EnterPlayMode(expectDomainReload: false);

        Check(!(bool)GetStatic(k_singleton, "m_ShuttingDown"), "session 2: Singleton<T> is not shutting down");
        Check(GetStatic(k_singleton, "m_Instance") == null, "session 2: Singleton<T> dropped the previous session's instance");
        Check(!(bool)GetStatic(k_saveManager, "m_initialized"), "session 2: the manager is uninitialized");
        Check(GetStatic(k_saveManager, "m_fileHandler") == null, "session 2: the previous session's file handler is gone");
        Check(SaveableCount == 0, "session 2: no saveables are registered");
        Check(FileScopeCount == 0, "session 2: no file scopes are remembered");
        Check(SaveManager.SaveSlotIndex == -1, "session 2: the slot index is back at -1");
        Check(!SaveManager.IsBusy && QueueCount == 0, "session 2: the queue is empty and idle");
        Check(queued.GetAwaiter().IsCompleted, "session 2: the abandoned request was released");
        bool cancelled = false;
        try { queued.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "session 2: the abandoned request observed cancellation");

        Check(SaveManager.Instance != null, "session 2: SaveManager.Instance is created again");
        SaveManager.RegisterSaveable(new TestSaveable());
        Check(SaveableCount == 1, "session 2: the saveable registered once, without an 'already exists' warning");
        Check((bool)GetStatic(k_saveManager, "m_initialized") && GetStatic(k_saveManager, "m_fileHandler") != null,
            "session 2: the manager initialized itself with a fresh file handler");

        var handler2 = ScriptableObject.CreateInstance<InMemoryFileHandler>();
        SetStatic(k_saveManager, "m_fileHandler", handler2);
        var save = SaveManager.Save(k_filename).GetAwaiter();
        double deadline = EditorApplication.timeSinceStartup + 10;
        while (!save.IsCompleted && EditorApplication.timeSinceStartup < deadline)
            yield return null;
        Check(save.IsCompleted, "session 2: a save completes");
        Check(handler2.Files.ContainsKey(k_filename), "session 2: the save was written");
        Check(!SaveManager.IsBusy, "session 2: the queue is idle after the save");

        yield return new ExitPlayMode();

        Assert.IsEmpty(m_failures, string.Join("\n", m_failures));
    }

    void Check(bool condition, string expectation)
    {
        if (!condition)
            m_failures.Add(expectation);
    }

    static object GetStatic(Type type, string field)
        => type.GetField(field, k_staticFlags).GetValue(null);

    static void SetStatic(Type type, string field, object value)
        => type.GetField(field, k_staticFlags).SetValue(null, value);

    static int SaveableCount => ((ICollection)GetStatic(k_saveManager, "m_saveables")).Count;
    static int FileScopeCount => ((ICollection)GetStatic(k_saveManager, "s_fileScopes")).Count;
    static int QueueCount => ((ICollection)GetStatic(k_saveManager, "m_fileOperationQueue")).Count;
}
