using System;
using VisualKeyloggerDetector.Core; // For DetectionResult
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace VisualKeyloggerDetector.Core.Utils
{
    public static class NotificationHelper
    {
        public static void ShowDetectionNotification(DetectionResult result)
        {
            if (result == null || !result.IsDetected) return;

            try
            {
                string toastXmlString =
                    $@"<toast activationType='foreground'>
                        <visual>
                            <binding template='ToastGeneric'>
                                <text>Potential Keylogger Detected!</text>
                                <text>Process: {result.ProcessName} (PID: {result.ProcessId})</text>
                                <text>Correlation: {result.Correlation:F4}</text>
                                <image placement='appLogoOverride' src='file:///{System.IO.Path.GetFullPath("Resources/warning_icon.png")}' hint-crop='circle'/>
                            </binding>
                        </visual>
                    </toast>";

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(toastXmlString);

                ToastNotification toast = new ToastNotification(doc);
                ToastNotificationManager.CreateToastNotifier("VisualKeyloggerDetector").Show(toast);

                Console.WriteLine($"Notification shown for PID: {result.ProcessId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to show notification: {ex.Message}");
            }
        }
    }
}
