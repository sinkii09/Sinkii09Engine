using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Provides utilities for formatting input binding display strings in a user-friendly way
    /// </summary>
    public static class BindingDisplayFormatter
    {
        #region Constants
        
        // Map common binding paths to user-friendly names
        private static readonly Dictionary<string, string> DeviceDisplayNames = new Dictionary<string, string>
        {
            { "keyboard", "Keyboard" },
            { "mouse", "Mouse" },
            { "gamepad", "Gamepad" },
            { "joystick", "Joystick" },
            { "touchscreen", "Touch" }
        };
        
        // Map common control paths to user-friendly names
        private static readonly Dictionary<string, string> ControlDisplayNames = new Dictionary<string, string>
        {
            // Keyboard
            { "leftarrow", "←" },
            { "rightarrow", "→" },
            { "uparrow", "↑" },
            { "downarrow", "↓" },
            { "leftshift", "Left Shift" },
            { "rightshift", "Right Shift" },
            { "leftctrl", "Left Ctrl" },
            { "rightctrl", "Right Ctrl" },
            { "leftalt", "Left Alt" },
            { "rightalt", "Right Alt" },
            { "space", "Space" },
            { "enter", "Enter" },
            { "escape", "Escape" },
            { "tab", "Tab" },
            { "backspace", "Backspace" },
            { "delete", "Delete" },
            { "home", "Home" },
            { "end", "End" },
            { "pageup", "Page Up" },
            { "pagedown", "Page Down" },
            { "insert", "Insert" },
            { "capslock", "Caps Lock" },
            { "numlock", "Num Lock" },
            { "scrolllock", "Scroll Lock" },
            { "printscreen", "Print Screen" },
            { "pause", "Pause" },
            
            // Mouse
            { "leftbutton", "LMB" },
            { "rightbutton", "RMB" },
            { "middlebutton", "MMB" },
            { "forwardbutton", "Mouse 4" },
            { "backbutton", "Mouse 5" },
            { "scroll/up", "Scroll Up" },
            { "scroll/down", "Scroll Down" },
            { "scroll/left", "Scroll Left" },
            { "scroll/right", "Scroll Right" },
            
            // Gamepad
            { "buttonSouth", "A" },
            { "buttonEast", "B" },
            { "buttonWest", "X" },
            { "buttonNorth", "Y" },
            { "leftShoulder", "LB" },
            { "rightShoulder", "RB" },
            { "leftTrigger", "LT" },
            { "rightTrigger", "RT" },
            { "leftStick", "Left Stick" },
            { "rightStick", "Right Stick" },
            { "leftStickPress", "L3" },
            { "rightStickPress", "R3" },
            { "dpad/up", "D-Pad Up" },
            { "dpad/down", "D-Pad Down" },
            { "dpad/left", "D-Pad Left" },
            { "dpad/right", "D-Pad Right" },
            { "start", "Start" },
            { "select", "Select" }
        };
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Format a binding path into a user-friendly display string
        /// </summary>
        public static string FormatBindingPath(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return "Unbound";
            
            try
            {
                // Use Unity's built-in formatter as base
                var displayString = InputControlPath.ToHumanReadableString(
                    bindingPath, 
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
                
                // Apply custom formatting
                return ApplyCustomFormatting(displayString, bindingPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BindingDisplayFormatter] Failed to format binding path '{bindingPath}': {ex.Message}");
                return ExtractControlName(bindingPath);
            }
        }
        
        /// <summary>
        /// Get a short display string for a binding (suitable for buttons)
        /// </summary>
        public static string GetShortDisplayString(string bindingPath)
        {
            var fullDisplay = FormatBindingPath(bindingPath);
            
            // Return shorter versions for common controls
            return fullDisplay switch
            {
                "Left Arrow" => "←",
                "Right Arrow" => "→",
                "Up Arrow" => "↑",
                "Down Arrow" => "↓",
                "Left Mouse Button" => "LMB",
                "Right Mouse Button" => "RMB",
                "Middle Mouse Button" => "MMB",
                "Left Shift" => "L-Shift",
                "Right Shift" => "R-Shift",
                "Left Ctrl" => "L-Ctrl",
                "Right Ctrl" => "R-Ctrl",
                "Space Bar" => "Space",
                _ => fullDisplay
            };
        }
        
        /// <summary>
        /// Get display string with device information
        /// </summary>
        public static string GetFullDisplayString(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return "Unbound";
            
            try
            {
                // Include device information
                return InputControlPath.ToHumanReadableString(
                    bindingPath, 
                    InputControlPath.HumanReadableStringOptions.None);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BindingDisplayFormatter] Failed to get full display string for '{bindingPath}': {ex.Message}");
                return bindingPath;
            }
        }
        
        /// <summary>
        /// Check if a binding path represents a composite binding
        /// </summary>
        public static bool IsCompositeBinding(string bindingPath)
        {
            return !string.IsNullOrEmpty(bindingPath) && bindingPath.Contains("/");
        }
        
        /// <summary>
        /// Get the device type from a binding path
        /// </summary>
        public static InputDeviceType GetDeviceTypeFromPath(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return InputDeviceType.Unknown;
            
            var lowerPath = bindingPath.ToLower();
            
            if (lowerPath.Contains("keyboard"))
                return InputDeviceType.Keyboard;
            if (lowerPath.Contains("mouse") || lowerPath.Contains("pointer"))
                return InputDeviceType.Mouse;
            if (lowerPath.Contains("gamepad") || lowerPath.Contains("joystick"))
                return InputDeviceType.Gamepad;
            if (lowerPath.Contains("touchscreen") || lowerPath.Contains("touch"))
                return InputDeviceType.Touch;
            
            return InputDeviceType.Unknown;
        }
        
        /// <summary>
        /// Get an icon name/identifier for a binding (for UI systems that support icons)
        /// </summary>
        public static string GetBindingIcon(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return "unbound";
            
            var deviceType = GetDeviceTypeFromPath(bindingPath);
            var lowerPath = bindingPath.ToLower();
            
            // Return icon identifiers that can be mapped to actual icons
            return deviceType switch
            {
                InputDeviceType.Mouse when lowerPath.Contains("leftbutton") => "mouse-left",
                InputDeviceType.Mouse when lowerPath.Contains("rightbutton") => "mouse-right",
                InputDeviceType.Mouse when lowerPath.Contains("middlebutton") => "mouse-middle",
                InputDeviceType.Mouse when lowerPath.Contains("scroll") => "mouse-scroll",
                InputDeviceType.Mouse => "mouse",
                
                InputDeviceType.Gamepad when lowerPath.Contains("buttonsouth") => "gamepad-a",
                InputDeviceType.Gamepad when lowerPath.Contains("buttoneast") => "gamepad-b",
                InputDeviceType.Gamepad when lowerPath.Contains("buttonwest") => "gamepad-x",
                InputDeviceType.Gamepad when lowerPath.Contains("buttonnorth") => "gamepad-y",
                InputDeviceType.Gamepad when lowerPath.Contains("leftstick") => "gamepad-left-stick",
                InputDeviceType.Gamepad when lowerPath.Contains("rightstick") => "gamepad-right-stick",
                InputDeviceType.Gamepad => "gamepad",
                
                InputDeviceType.Keyboard when lowerPath.Contains("arrow") => "keyboard-arrow",
                InputDeviceType.Keyboard when lowerPath.Contains("space") => "keyboard-space",
                InputDeviceType.Keyboard when lowerPath.Contains("enter") => "keyboard-enter",
                InputDeviceType.Keyboard => "keyboard",
                
                InputDeviceType.Touch => "touch",
                _ => "unknown"
            };
        }
        
        /// <summary>
        /// Format multiple bindings for the same action (e.g., "WASD" for movement)
        /// </summary>
        public static string FormatMultipleBindings(IEnumerable<string> bindingPaths)
        {
            if (bindingPaths == null)
                return "Unbound";
            
            var formattedBindings = new List<string>();
            foreach (var path in bindingPaths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    formattedBindings.Add(GetShortDisplayString(path));
                }
            }
            
            return formattedBindings.Count > 0 ? string.Join(" + ", formattedBindings) : "Unbound";
        }
        
        #endregion
        
        #region Private Helpers
        
        private static string ApplyCustomFormatting(string displayString, string originalPath)
        {
            // Apply custom display name mappings
            var lowerDisplay = displayString.ToLower();
            
            foreach (var mapping in ControlDisplayNames)
            {
                if (lowerDisplay.Contains(mapping.Key))
                {
                    return displayString.Replace(mapping.Key, mapping.Value);
                }
            }
            
            // Clean up common formatting issues
            displayString = displayString.Replace("  ", " ").Trim();
            
            // Capitalize first letter
            if (displayString.Length > 0)
            {
                displayString = char.ToUpper(displayString[0]) + displayString.Substring(1);
            }
            
            return displayString;
        }
        
        private static string ExtractControlName(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return "Unbound";
            
            // Extract the last part of the path as a fallback
            var parts = bindingPath.Split('/');
            if (parts.Length > 0)
            {
                var controlName = parts[parts.Length - 1];
                
                // Remove angle brackets and device prefixes
                controlName = controlName.Replace("<", "").Replace(">", "");
                
                // Capitalize and return
                return char.ToUpper(controlName[0]) + controlName.Substring(1);
            }
            
            return bindingPath;
        }
        
        #endregion
    }
}