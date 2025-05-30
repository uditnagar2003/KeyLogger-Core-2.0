using System.Management; // Requires adding a reference to System.Management.dll

namespace VisualKeyloggerDetector // Main namespace
{
    
    public static class ProcessMonitor
    {
        
        public static async Task<List<ProcessInfoData>> GetAllProcessesInfoAsync()
        {
            var processes = new List<ProcessInfoData>();
            // Select only the properties we need for better performance
            string wmiQuery = "SELECT ProcessId, Name, ExecutablePath, WriteTransferCount FROM Win32_Process";

            // Run WMI query on a background thread to avoid blocking UI/main thread
            await Task.Run(() =>
            {
                ManagementObjectSearcher searcher = null;
                ManagementObjectCollection collection = null;
                try
                {
                    searcher = new ManagementObjectSearcher(wmiQuery);
                    collection = searcher.Get(); // Execute the query

                    foreach (ManagementBaseObject obj in collection)
                    {
                        using (obj) // Ensure each ManagementObject is disposed
                        {
                            // Safely get properties, checking for null or DBNull
                            ulong writeTransferCount = 0;
                            object writeCountObj = obj["WriteTransferCount"];
                            if (writeCountObj != null && writeCountObj != DBNull.Value)
                            {
                                // Handle potential conversion errors, though ulong should be safe here
                                try { writeTransferCount = Convert.ToUInt64(writeCountObj); }
                                catch { /* Ignore conversion error, keep 0 */ }
                            }

                            uint processId = 0;
                            object pidObj = obj["ProcessId"];
                            if (pidObj != null && pidObj != DBNull.Value)
                            {
                                try { processId = Convert.ToUInt32(pidObj); }
                                catch { /* Ignore conversion error, keep 0 */ }
                            }

                            // Only add if we got a valid ProcessId
                            if (processId != 0)
                            {
                                processes.Add(new ProcessInfoData
                                {
                                    Id = processId,
                                    Name = obj["Name"] as string ?? string.Empty,
                                    ExecutablePath = obj["ExecutablePath"] as string, // Path can be null
                                    WriteCount = writeTransferCount
                                });
                            }
                        }
                    }
                }
                // Catch specific WMI exceptions
                catch (ManagementException ex)
                {
                    Console.WriteLine($"WMI Error in GetAllProcessesInfoAsync: {ex.Message}");
                    // Depending on requirements, you might want to throw, return empty, or partial list
                    // For now, we'll let it throw to indicate a significant issue.
                    throw;
                }
                // Catch other potential exceptions during enumeration
                catch (Exception ex)
                {
                    Console.WriteLine($"Error collecting process info in GetAllProcessesInfoAsync: {ex.Message}");
                    throw; // Re-throw other critical errors
                }
                finally
                {
                    // Ensure WMI objects are disposed even if errors occur
                    collection?.Dispose();
                    searcher?.Dispose();
                }
            }); // End Task.Run

            return processes;
        }
        public static async Task<ProcessInfoData> GetProcessById( int id)
        {
            var processes = new ProcessInfoData();
            // Select only the properties we need for better performance
            string wmiQuery = $"SELECT ProcessId, Name, ExecutablePath, WriteTransferCount FROM Win32_Process WHERE ProcessId = {id}";

            // Run WMI query on a background thread to avoid blocking UI/main thread
            await Task.Run(() =>
            {
                ManagementObjectSearcher searcher = null;
                ManagementObjectCollection collection = null;
                try
                {
                    searcher = new ManagementObjectSearcher(wmiQuery);
                    collection = searcher.Get(); // Execute the query

                    foreach (ManagementBaseObject obj in collection)
                    {
                        using (obj) // Ensure each ManagementObject is disposed
                        {
                            // Safely get properties, checking for null or DBNull
                            ulong writeTransferCount = 0;
                            object writeCountObj = obj["WriteTransferCount"];
                            if (writeCountObj != null && writeCountObj != DBNull.Value)
                            {
                                // Handle potential conversion errors, though ulong should be safe here
                                try { writeTransferCount = Convert.ToUInt64(writeCountObj); }
                                catch { /* Ignore conversion error, keep 0 */ }
                            }

                            uint processId = 0;
                            object pidObj = obj["ProcessId"];
                            if (pidObj != null && pidObj != DBNull.Value)
                            {
                                try { processId = Convert.ToUInt32(pidObj); }
                                catch { /* Ignore conversion error, keep 0 */ }
                            }

                            // Only add if we got a valid ProcessId
                            if (processId != 0)
                            {
                                processes = new ProcessInfoData
                                {
                                    Id = processId,
                                    Name = obj["Name"] as string ?? string.Empty,
                                    ExecutablePath = obj["ExecutablePath"] as string, // Path can be null
                                    WriteCount = writeTransferCount
                                };
                            }
                        }
                    }
                }
                // Catch specific WMI exceptions
                catch (ManagementException ex)
                {
                    Console.WriteLine($"WMI Error in GetAllProcessesInfoAsync: {ex.Message}");
                    // Depending on requirements, you might want to throw, return empty, or partial list
                    // For now, we'll let it throw to indicate a significant issue.
                    throw;
                }
                // Catch other potential exceptions during enumeration
                catch (Exception ex)
                {
                    Console.WriteLine($"Error collecting process info in GetAllProcessesInfoAsync: {ex.Message}");
                    throw; // Re-throw other critical errors
                }
                finally
                {
                    // Ensure WMI objects are disposed even if errors occur
                    collection?.Dispose();
                    searcher?.Dispose();
                }
            }); // End Task.Run

            return processes;
        }
    }

    public class ProcessInfoData
    {
        /// <summary> Gets or sets the process name. </summary>
        public string Name { get; set; }
        /// <summary> Gets or sets the full path to the process executable, if available. </summary>
        public string ExecutablePath { get; set; }
        /// <summary> Gets or sets the total number of bytes written by the process (WriteTransferCount). </summary>
        public ulong WriteCount { get; set; }
        /// <summary> Gets or sets the unique process identifier (PID). </summary>
        public uint Id { get; set; }
    }
}