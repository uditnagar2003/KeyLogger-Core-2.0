using System.Runtime.InteropServices;

namespace VisualKeyloggerDetector // Main namespace
{
    
    public static class KeyInputInjector
    {
        // Win32 API Imports for SendInput
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        // Used to map characters to virtual key codes and shift states
        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        // Structures needed for SendInput
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;      // Virtual-key code
            public ushort wScan;    // Hardware scan code
            public uint dwFlags;  // Flags specifying various aspects of keystroke
            public uint time;     // Time stamp for the event, in milliseconds
            public IntPtr dwExtraInfo; // Additional info associated with the keystroke
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }


        // Constants for INPUT structure type and KEYBDINPUT flags
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001; // Key is extended key
        private const uint KEYEVENTF_KEYUP = 0x0002;       // Key is being released
        private const uint KEYEVENTF_UNICODE = 0x0004;     // Using Unicode character
        private const uint KEYEVENTF_SCANCODE = 0x0008;    // Using hardware scan code

        // Virtual Key Codes (subset)
        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_MENU = 0x12; // ALT key


        public static void SendCharacter(char character)
        {
            short vkScanResult = VkKeyScan(character);

            // Extract virtual key code (low byte) and shift state (high byte)
            ushort vk = (ushort)(vkScanResult & 0xFF);
            byte shiftState = (byte)((vkScanResult >> 8) & 0xFF);

            // Build the list of input events
            var inputs = new List<INPUT>();

            // Check if SHIFT needs to be pressed
            if ((shiftState & 1) != 0) // Check SHIFT bit
            {
                inputs.Add(CreateKeyInput(VK_SHIFT, 0, 0)); // Press Shift
            }
            // Check if CTRL needs to be pressed (unlikely for simple chars, but example)
            if ((shiftState & 2) != 0) // Check CTRL bit
            {
                inputs.Add(CreateKeyInput(VK_CONTROL, 0, 0)); // Press Ctrl
            }
            // Check if ALT needs to be pressed (unlikely for simple chars, but example)
            if ((shiftState & 4) != 0) // Check ALT bit
            {
                inputs.Add(CreateKeyInput(VK_MENU, 0, 0)); // Press Alt
            }

            // Add the main character key press and release
            inputs.Add(CreateKeyInput(vk, 0, 0));                 // Press character key
            inputs.Add(CreateKeyInput(vk, 0, KEYEVENTF_KEYUP));   // Release character key

            // Release modifier keys in reverse order
            if ((shiftState & 4) != 0) // Release ALT
            {
                inputs.Add(CreateKeyInput(VK_MENU, 0, KEYEVENTF_KEYUP));
            }
            if ((shiftState & 2) != 0) // Release CTRL
            {
                inputs.Add(CreateKeyInput(VK_CONTROL, 0, KEYEVENTF_KEYUP));
            }
            if ((shiftState & 1) != 0) // Release SHIFT
            {
                inputs.Add(CreateKeyInput(VK_SHIFT, 0, KEYEVENTF_KEYUP));
            }

            // Send the inputs
            INPUT[] inputArray = inputs.ToArray();
            uint result = SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf(typeof(INPUT)));

            if (result == 0)
            {
                // Get error code and throw exception
                int errorCode = Marshal.GetLastWin32Error();
                throw new Exception($"SendInput failed with error code: {errorCode}");
            }

            // Small delay between distinct character sends can sometimes improve reliability in fast loops
            Thread.Sleep(5); // Adjust delay as needed, or remove if unnecessary
        }

        
        private static INPUT CreateKeyInput(ushort vk, ushort scan, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = scan,
                        dwFlags = flags,
                        time = 0, // System will provide timestamp
                        dwExtraInfo = GetMessageExtraInfo() // Retrieve extra message info
                    }
                }
            };
        }
    }
}