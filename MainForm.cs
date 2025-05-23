using System;
using System.Collections.Generic;
using System.Windows.Forms;
using VisualKeyloggerDetector.Core.Utils;
using VisualKeyloggerDetector.Core; // Add Core namespace
using VisualKeyloggerDetector.Core.PatternGeneration; // Add PatternGeneration namespace
using System.Linq; // Added for Linq

namespace VisualKeyloggerDetector
{
    /// <summary>
    /// The main window for the Visual Keylogger Detector application.
    /// Handles user interaction and orchestrates the detection experiment via ExperimentController.
    /// </summary>
    public partial class MainForm : Form
    {
        private ExperimentController _experimentController;
        private ExperimentConfiguration _currentConfig; // Store config

        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            InitializeExperiment(); // Setup controller on startup
        }

        /// <summary>
        /// Initializes or re-initializes the ExperimentController with current settings.
        /// Selects the pattern generation algorithm and subscribes to controller events.
        /// </summary>
        private void InitializeExperiment()
        {
            // Create configuration (could be loaded from UI/settings later)
            _currentConfig = new ExperimentConfiguration();

            // --- Choose a pattern generation algorithm ---
            // Uncomment the desired algorithm:
            IPatternGeneratorAlgorithm algorithm=null;
            Random rnd = new Random();
            int algo = rnd.Next(1, 4);
            switch(algo)
            { 
                case 1:
                        algorithm= new RandomPatternAlgorithm();
                    break;
                case 2:
                     algorithm = new RandomFixedRangePatternAlgorithm();
                    break;
                case 3:

                     algorithm = new ImpulsePatternAlgorithm(); // Often better variability
                    break;
                case 4:   
                    // IPatternGeneratorAlgorithm algorithm = new ImpulsePatternAlgorithm();
                       algorithm = new SineWavePatternAlgorithm();
                    break;
            }
            // Dispose previous instance if any, before creating a new one
            _experimentController?.Dispose();
            _experimentController = new ExperimentController(_currentConfig,algorithm);

            // Subscribe to events from the controller to update the UI
            _experimentController.StatusUpdated += ExperimentController_StatusUpdated;
            _experimentController.ProgressUpdated += ExperimentController_ProgressUpdated;
            _experimentController.ExperimentCompleted += ExperimentController_ExperimentCompleted;
            _experimentController.KeyloggerDetected += ExperimentController_KeyloggerDetected; // Subscribe
            // Initial UI state
            UpdateStatus($"Ready. Using {algorithm.GetType().Name}.");
            SetButtonsEnabled(true, false); // Initial state: Start enabled, Stop disabled
            UpdateProgressBar(0, 1); // Reset progress bar state
            toolStripProgressBar1.Visible = false; // Hide progress bar initially
        }


        // --- Event Handlers from ExperimentController ---

        /// <summary>
        /// Handles the StatusUpdated event from the ExperimentController.
        /// Updates the status label on the UI thread.
        /// </summary>
        private void ExperimentController_StatusUpdated(object sender, string status)
        {
            UpdateStatus(status);
        }

        /// <summary>
        /// Handles the ProgressUpdated event from the ExperimentController.
        /// Updates the progress bar on the UI thread.
        /// </summary>
        private void ExperimentController_ProgressUpdated(object sender, (int current, int total) progress)
        {
            UpdateProgressBar(progress.current, progress.total);
        }

        /// <summary>
        /// Handles the ExperimentCompleted event from the ExperimentController.
        /// Updates button states, hides the progress bar, and shows a summary message box.
        /// </summary>
        private void ExperimentController_ExperimentCompleted(object sender, List<DetectionResult> results)
        {
            // Ensure UI updates are on the correct thread
            if (InvokeRequired)
            {
                Invoke(new Action(() => ExperimentCompletedUI(results)));
            }
            else
            {
                ExperimentCompletedUI(results);
            }
        }

        /// <summary>
        /// Contains the UI update logic for when the experiment completes.
        /// Should be called on the UI thread.
        /// </summary>
        /// <param name="results">The results from the completed experiment.</param>
        private void ExperimentCompletedUI(List<DetectionResult> results)
        {
            SetButtonsEnabled(true, false); // Re-enable Start, disable Stop
            UpdateProgressBar(0, 1); // Reset progress bar
            toolStripProgressBar1.Visible = false; // Hide progress bar

            int detectedCount = results?.Count(r => r.IsDetected) ?? 0; // Handle null results list defensively
            string message = $"Detection complete. Found {detectedCount} potential keylogger(s) matching criteria.\n\nSee '{_currentConfig.ResultsFilePath}' for details.";
            MessageBoxIcon icon = detectedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information;

            MessageBox.Show(this, message, "Detection Complete", MessageBoxButtons.OK, icon); // Specify owner window
            UpdateStatus("Detection Complete. Ready."); // Final status
        }

        // --- UI Update Helpers ---

        /// <summary>
        /// Updates the text of the status label, ensuring it runs on the UI thread.
        /// </summary>
        /// <param name="message">The message to display.</param>
        private void UpdateStatus(string message)
        {
            if (statusStrip1.InvokeRequired)
            {
                // Use BeginInvoke for potentially better responsiveness if status updates are frequent
                statusStrip1.BeginInvoke(new Action(() => toolStripStatusLabel1.Text = message));
            }
            else
            {
                toolStripStatusLabel1.Text = message;
            }
        }

        /// <summary>
        /// Updates the value and visibility of the progress bar, ensuring it runs on the UI thread.
        /// </summary>
        /// <param name="value">The current progress value.</param>
        /// <param name="maximum">The maximum progress value.</param>
        private void UpdateProgressBar(int value, int maximum)
        {
            // ToolStripItems don't have InvokeRequired, check the parent StatusStrip
            if (statusStrip1.InvokeRequired)
            {
                statusStrip1.BeginInvoke(new Action(() => {
                    toolStripProgressBar1.Maximum = Math.Max(1, maximum); // Ensure maximum is at least 1
                    toolStripProgressBar1.Value = Math.Max(0, Math.Min(value, toolStripProgressBar1.Maximum)); // Clamp value
                    toolStripProgressBar1.Visible = (maximum > 0 && value < maximum); // Show only when running and max is valid
                }));
            }
            else
            {
                toolStripProgressBar1.Maximum = Math.Max(1, maximum);
                toolStripProgressBar1.Value = Math.Max(0, Math.Min(value, toolStripProgressBar1.Maximum));
                toolStripProgressBar1.Visible = (maximum > 0 && value < maximum);
            }
        }

        /// <summary>
        /// Enables or disables the Start and Stop buttons, ensuring it runs on the UI thread.
        /// </summary>
        /// <param name="startEnabled">True to enable the Start button, false to disable.</param>
        /// <param name="stopEnabled">True to enable the Stop button, false to disable.</param>
        private void SetButtonsEnabled(bool startEnabled, bool stopEnabled)
        {
            // Check InvokeRequired on the form itself or a control like startButton
            if (startButton.InvokeRequired)
            {
                startButton.BeginInvoke(new Action(() => {
                    startButton.Enabled = startEnabled;
                    stopButton.Enabled = stopEnabled;
                }));
            }
            else
            {
                startButton.Enabled = startEnabled;
                stopButton.Enabled = stopEnabled;
            }
        }

        // --- Button Click Handlers ---

        /// <summary>
        /// Handles the Click event for the Start button.
        /// Disables Start, enables Stop, and asynchronously starts the experiment.
        /// </summary>
        private async void startButton_Click(object sender, EventArgs e)
        {
            SetButtonsEnabled(false, true); // Disable Start, Enable Stop
            UpdateProgressBar(0, 1); // Reset progress bar state
            toolStripProgressBar1.Visible = true;
            UpdateStatus("Starting experiment...");

            try
            {
                // Ensure controller is initialized
                if (_experimentController == null)
                {
                    InitializeExperiment();
                }
                // Run the experiment asynchronously
                await _experimentController.StartExperimentAsync();
            }
            catch (Exception ex) // Catch unexpected errors during the start sequence or experiment itself
            {
                MessageBox.Show(this, $"An error occurred during the experiment: {ex.Message}", "Experiment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus($"Error: {ex.Message}");
                // Ensure UI is reset correctly on error
                if (!_experimentController.IsRunning) // Check if controller already reset state
                {
                    SetButtonsEnabled(true, false);
                    toolStripProgressBar1.Visible = false;
                }
            }
        }

        /// <summary>
        /// Handles the Click event for the Stop button.
        /// Requests cancellation of the running experiment.
        /// </summary>
        private void stopButton_Click(object sender, EventArgs e)
        {
            _experimentController?.StopExperiment();
            // UI updates (status, buttons) will be handled by the cancellation/completion events from the controller.
            UpdateStatus("Stop requested..."); // Give immediate feedback
            stopButton.Enabled = false; // Disable stop button immediately after clicking
        }

        private void ExperimentController_KeyloggerDetected(object sender, DetectionResult result)
        {
            // Ensure UI updates are on the correct thread
            if (InvokeRequired)
            {
                Invoke(new Action(() => NotificationHelper.ShowDetectionNotification(result)));
            }
            else
            {
                NotificationHelper.ShowDetectionNotification(result);
            }
            // Here you could also update a UI list of detected items
        }

        // --- Form Load/Closing ---

        /// <summary>
        /// Handles the Load event of the main form.
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Initial status is set in InitializeExperiment
        }

        /// <summary>
        /// Handles the FormClosing event.
        /// Ensures the experiment is stopped and resources are released before the form closes.
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Request stop if running, then dispose
            _experimentController?.StopExperiment();
            _experimentController?.Dispose();
        }

        // Remember to add ToolStripProgressBar to MainForm.Designer.cs
        // Example additions to InitializeComponent():
        // this.toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
        // ...
        // this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
        // this.toolStripStatusLabel1,
        // this.toolStripProgressBar1}); // Add it to the items collection
        // ...
        // // toolStripProgressBar1
        // //
        // this.toolStripProgressBar1.Name = "toolStripProgressBar1";
        // this.toolStripProgressBar1.Size = new System.Drawing.Size(100, 16); // Adjust size as needed
        // this.toolStripProgressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
        // this.toolStripProgressBar1.Visible = false; // Initially hidden
    }
}
