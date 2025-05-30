﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

namespace AIRefactored.AI.Core
{
    using System;
    using System.Text.RegularExpressions;
    using Fika.Headless.Classes;
    using UnityEngine;

    /// <summary>
    /// Detects if the client is running in FIKA Headless Host mode (i.e., -batchmode or -nographics).
    /// Allows runtime branching for systems that must avoid Unity graphics or main-thread dependencies.
    /// Bulletproof: All logic is strictly guarded; no error can ever break mod flow.
    /// </summary>
    public static class FikaHeadlessDetector
    {
        #region Fields

        private static readonly bool _isHeadless;
        private static readonly string _raidLocation;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether the current client is in headless or batch mode.
        /// </summary>
        public static bool IsHeadless => _isHeadless;

        /// <summary>
        /// Gets the map name if parsed from FIKA headless arguments.
        /// Returns string.Empty if not in headless or if parsing failed.
        /// </summary>
        public static string RaidLocationName => _raidLocation;

        /// <summary>
        /// Gets a value indicating whether the headless environment has fully parsed its arguments.
        /// Used to delay logic until startup args are loaded.
        /// </summary>
        public static bool IsReady => _isHeadless && !string.IsNullOrEmpty(_raidLocation);

        /// <summary>
        /// Gets a value indicating whether the raid loading phase has started.
        /// </summary>
        public static bool HasRaidStarted()
        {
            return !_isHeadless || HeadlessRaidControllerExists();
        }

        #endregion

        #region Static Constructor

        static FikaHeadlessDetector()
        {
            _isHeadless = false;
            _raidLocation = string.Empty;

            try
            {
                string cmd = Environment.CommandLine;
                _isHeadless = Application.isBatchMode || cmd.IndexOf("-nographics", StringComparison.OrdinalIgnoreCase) >= 0;

                if (_isHeadless)
                {
                    _raidLocation = TryParseRaidLocationFromArgs();
                }
            }
            catch
            {
                _isHeadless = false;
                _raidLocation = string.Empty;
            }
        }

        #endregion

        #region Helpers

        private static bool HeadlessRaidControllerExists()
        {
            try
            {
                return GameObject.FindObjectOfType<HeadlessRaidController>() != null;
            }
            catch
            {
                return false;
            }
        }

        private static string TryParseRaidLocationFromArgs()
        {
            try
            {
                string cmd = Environment.CommandLine;
                Match match = Regex.Match(cmd, "\"location\"\\s*:\\s*\"(.*?)\"", RegexOptions.IgnoreCase);

                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            catch
            {
                // Silent fail
            }

            return string.Empty;
        }

        #endregion
    }
}
