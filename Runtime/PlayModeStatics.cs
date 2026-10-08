// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Buck.SaveAsync
{
    /// <summary>
    /// Runs reset actions at the start of every Play session, so code behaves the same whether or
    /// not the editor reloads the scripting domain on Play (Edit > Project Settings > Editor > Enter
    /// Play Mode Settings). Unity's CoreCLR runtime removes domain reload altogether, so this is also
    /// what keeps that code working there.
    ///
    /// It exists for generic types. Unity does not invoke RuntimeInitializeOnLoadMethod inside a
    /// generic type, so each closed generic type (Singleton&lt;T&gt;, for one) registers a reset action
    /// from its static constructor and this class runs them all. A non-generic type resets its own
    /// state with its own RuntimeInitializeOnLoadMethod instead.
    ///
    /// The resets run at SubsystemRegistration, the earliest stage, before any scene object's Awake.
    /// In a player, and in an editor that still reloads the domain, there is nothing stale to reset.
    /// </summary>
    internal static class PlayModeStatics
    {
        static readonly List<Action> s_resets = new List<Action>();
        static readonly object s_lock = new object();

        /// <summary>
        /// Registers an action that puts a type's statics back to their initial values. Call it once
        /// per closed generic type, from its static constructor. The list is never cleared: a
        /// registration has to outlive every Play session.
        /// </summary>
        public static void Register(Action reset)
        {
            if (reset == null)
                return;

            // A static constructor runs on whichever thread first touches the type.
            lock (s_lock)
            {
                if (!s_resets.Contains(reset))
                    s_resets.Add(reset);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession()
        {
            Action[] resets;
            lock (s_lock)
                resets = s_resets.ToArray();

            // One failing reset must not stop the others.
            foreach (var reset in resets)
            {
                try
                {
                    reset();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
