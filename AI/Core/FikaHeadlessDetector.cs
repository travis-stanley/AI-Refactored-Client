#nullable enable

using System;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Detects if the client is running in FIKA Headless Host mode (i.e. -batchmode -nographics).
    /// Allows safe runtime branching for logic that must avoid Unity-dependent systems.
    /// </summary>
    public static class FikaHeadlessDetector
    {
        private static readonly bool _isHeadless;

        /// <summary>
        /// True if this client is running under headless (batchmode/nographics).
        /// </summary>
        public static bool IsHeadless => _isHeadless;

        static FikaHeadlessDetector()
        {
            _isHeadless = Application.isBatchMode || Environment.CommandLine.Contains("-nographics");
        }
    }
}
