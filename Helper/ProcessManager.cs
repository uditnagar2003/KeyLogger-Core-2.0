// Add this file to keylogger core project (e.g., Core/Utils/ProcessManager.cs)
using System.Runtime.InteropServices;

namespace VisualKeyloggerDetector.Core.Utils
{
    public static class ProcessManager
    {
        // P/Invoke signatures
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000,
            SuspendResume = 0x0800 // Required for NtSuspendProcess/NtResumeProcess
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            ProcessAccessFlags processAccess,
            bool bInheritHandle,
            uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

       
        public static bool SuspendProcess(uint processId)
        {
            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(ProcessAccessFlags.SuspendResume, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    Console.WriteLine($"Failed to open process {processId}. Error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                int ntStatus = NtSuspendProcess(hProcess);
                if (ntStatus != 0) // 0 is STATUS_SUCCESS
                {
                    Console.WriteLine($"Failed to suspend process {processId}. NTSTATUS: {ntStatus:X}");
                    return false;
                }

                Console.WriteLine($"Process {processId} suspended successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception suspending process {processId}: {ex.Message}");
                return false;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

       
        
        public static bool TerminateProcess(uint processId)
        {
            IntPtr hProcess = IntPtr.Zero;
            try
            {
                // Open process with Terminate rights
                hProcess = OpenProcess(ProcessAccessFlags.Terminate, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    Console.WriteLine($"Failed to open process {processId} for termination. Error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                // Terminate the process
                // Use TerminateProcess from kernel32.dll
                if (!TerminateProcessNative(hProcess, 1)) // Exit code 1 indicates termination by this tool
                {
                    Console.WriteLine($"Failed to terminate process {processId}. Error: {Marshal.GetLastWin32Error()}");
                    return false;
                }


                Console.WriteLine($"Process {processId} terminated successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception terminating process {processId}: {ex.Message}");
                return false;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcessNative(IntPtr hProcess, uint uExitCode);
    }
}