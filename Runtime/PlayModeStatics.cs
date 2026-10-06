// MIT License - Copyright (c) 2025 BUCK Design LLC - https://github.com/buck-co

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Buck.SaveAsync
{
    /// <summary>
    /// Restores the package's statics at the start of every Play session, so Save Async behaves
    /// the same whether or not the editor reloads the scripting domain on Play (Edit > Project
    /// Settings > Editor > Enter Play Mode Settings). Unity's CoreCLR runtime removes domain
    /// reload altogether, so this is also what keeps the package working there.
    ///
    /// Unity does not invoke RuntimeInitializeOnLoadMethod inside generic types, so each closed
    /// Singleton&lt;T&gt; registers a reset action from its static constructor and this class runs
    /// them all at SubsystemRegistration, the earliest stage, before any scene object's Awake.
    /// Everything here is a no-op in a player and in an editor that still reloads the domain:
    /// there is nothing stale to reset at that point.
    /// </summary>
    internal static class PlayModeStatics
    {
        static readonly List<Action> m_resets = new List<Action>();

        /// <summary>
        /// Registers an action that puts a type's statics back to their initial values. The list
        /// itself is never cleared, since a registration has to outlive every Play session.
        /// </summary>
        public static void Register(Action reset)
        {
            if (reset != null && !m_resets.Contains(reset))
                m_resets.Add(reset);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession()
        {
            for (int i = 0; i < m_resets.Count; i++)
                m_resets[i]();
        }
    }
}
