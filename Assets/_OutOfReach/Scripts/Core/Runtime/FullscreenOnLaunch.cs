using System;
using UnityEngine;

namespace OutOfReach.Core.Runtime
{
    /// <summary>
    /// Apoliak, 14 Nov 2026: the game always starts in full screen. Player Settings already say Fullscreen Window at the
    /// native resolution, but a Windows player remembers the last window mode per machine (the registry value
    /// "Screenmanager Fullscreen mode"), so a machine that once ran a build in a window starts every later build in a
    /// window again. Before the first scene loads, a standalone player is therefore switched to borderless full screen
    /// at the display's native resolution, every launch. Nothing happens in the editor (Play mode and tests keep the
    /// Game view). An explicit window request on the command line (-screen-fullscreen, -window-mode, -popupwindow or
    /// -windowed) is respected, for capture and debugging. Alt+Enter still toggles during play.
    /// </summary>
    public static class FullscreenOnLaunch
    {
        private static readonly string[] WindowArguments = { "-screen-fullscreen", "-window-mode", "-popupwindow", "-windowed" };

        /// <summary>True when a launch should be switched to full screen: a player, not the editor, and no explicit window request.</summary>
        public static bool ShouldForce(bool isEditor, string[] commandLine)
        {
            if (isEditor)
            {
                return false;
            }

            if (commandLine != null)
            {
                foreach (string argument in commandLine)
                {
                    foreach (string window in WindowArguments)
                    {
                        if (string.Equals(argument, window, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (!ShouldForce(Application.isEditor, Environment.GetCommandLineArgs()))
            {
                return;
            }

            int width = Display.main.systemWidth;
            int height = Display.main.systemHeight;
            if (width > 0 && height > 0)
            {
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            }
        }
    }
}
