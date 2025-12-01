/* ======================================================================================================================== */
/* FILE             : logsPage.xaml.cs                                                                                      */
/* PROJECT          : Software Quality Final Project Milestone 2                                                            */
/* NAMESPACE        : GroundTerminalApp                                                                                    */
/* PROGRAMMER       : Ermiyas (Endalkachew) Gulti, Mher Keshishian, Quang Minh Vu, Saje’- Antoine Rose                      */
/* FIRST VERSION    : 2025-11-22                                                                                            */
/* DESCRIPTION      : Defines the logsPage window used to view, filter, and manage FDMS system logs from SQL Server.       */
/* ======================================================================================================================== */


using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GroundTerminalApp
{
    public partial class logsPage : Window
    {
        private const int AUTO_REFRESH_INTERVAL = 30; // seconds

        private ObservableCollection<LogEntry> logEntries;
        private ObservableCollection<LogEntry> filteredLogEntries;

        private DispatcherTimer refreshTimer;
        private DispatcherTimer connectionCheckTimer;

        private bool isConnected = false;
        // I am adding this because i modified search page to accept window as parameter so logsPage needs a reference to search
        private SearchingPageApp searchingPage;

        SearchingPageApp searchingPageInstance;
        FDMSDashboard dashboard;


		/// <summary>
		/// Initializes the logsPage window, data collections, event handlers, and timers.
		/// </summary>
		/// <param name="dashboardInstance">Reference to the main FDMS dashboard window.</param>
		public logsPage(FDMSDashboard dashboardInstance)
        {
            InitializeComponent();
            InitializeData();
            SetupEventHandlers();
            InitializeTimers();

            CmbLogLevel.SelectedIndex = 0;

            searchingPage = searchingPageInstance;
            dashboard = dashboardInstance;
        }

        /// <summary>
        /// Initializes the data collections and sets up the data binding for the log entries.
        /// </summary>
        private void InitializeData()
        {
            logEntries = new ObservableCollection<LogEntry>();
            filteredLogEntries = new ObservableCollection<LogEntry>();
            LogsDataGrid.ItemsSource = filteredLogEntries;

            searchingPage = searchingPageInstance;
        }

		/// <summary>
		/// Wires up UI event handlers for search, log-level filters, and toolbar buttons.
		/// </summary>
		private void SetupEventHandlers()
        {
            TxtSearch.TextChanged += TxtSearch_TextChanged;
            CmbLogLevel.SelectionChanged += CmbLogLevel_SelectionChanged;

            var refreshButton = FindButtonByContent("Refresh");
            if (refreshButton != null)
                refreshButton.Click += RefreshButton_Click;

            var clearButton = FindButtonByContent("Clear Logs");
            if (clearButton != null)
                clearButton.Click += ClearLogsButton_Click;

            var searchQueryBtn = FindButtonByContent("Search & Query");
            if (searchQueryBtn != null)
                searchQueryBtn.Click += NavigateToSearchQuery;

            var userBtn = FindButtonByContent("User");
            if (userBtn != null)
                userBtn.Click += NavigateToUser;
        }

        /// <summary>
        /// Initializes and starts the timers used for periodic database operations.
        /// </summary>
		private void InitializeTimers()
        {
            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AUTO_REFRESH_INTERVAL)
            };
            refreshTimer.Tick += async (s, e) => await LoadLogsFromDatabase();
            refreshTimer.Start();

            connectionCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            connectionCheckTimer.Tick += async (s, e) => await CheckDatabaseConnection();
            connectionCheckTimer.Start();

            // Initial load
            Task.Run(async () =>
            {
                await CheckDatabaseConnection();
                await LoadLogsFromDatabase();
            });
        }

		/// <summary>
		/// Tests the SQL database connection, updates the connection status UI, and logs any errors.
		/// </summary>
		/// <returns>True if the database connection succeeds; otherwise, false.</returns>
		private async Task<bool> CheckDatabaseConnection()
        {
            try
            {
                await Task.Run(() =>
                {
                    using (SqlConnection conn = FDMSDashboard.ServerConnector.GetConnection())
                    {
                        conn.Open();
                        isConnected = true;
                    }
                });

                Dispatcher.Invoke(() =>
                {
                    dbOnlineIcon.Visibility = Visibility.Visible;
                    dbOfflineIcon.Visibility = Visibility.Collapsed;
                    dbConnectionStatusLbl.Content = "ONLINE";
                    dbConnectionStatusLbl.Foreground = System.Windows.Media.Brushes.Green;
                });

                return true;
            }
            catch (Exception ex)
            {
                isConnected = false;

                Dispatcher.Invoke(() =>
                {
                    dbOnlineIcon.Visibility = Visibility.Collapsed;
                    dbOfflineIcon.Visibility = Visibility.Visible;
                    dbConnectionStatusLbl.Content = "OFFLINE";
                    dbConnectionStatusLbl.Foreground = System.Windows.Media.Brushes.Red;
                });

                LogError("Database Connection", ex.Message);
                return false;
            }
        }

		/// <summary>
		/// Asynchronously loads the latest system logs from the database and refreshes the bound collections.
		/// </summary>
		/// <returns>A task that represents the asynchronous log loading operation.</returns>
		private async Task LoadLogsFromDatabase()
        {
            if (!isConnected)
            {
                await CheckDatabaseConnection();
                if (!isConnected) return;
            }

            try
            {
                var logs = await Task.Run(() =>
                {
                    var logList = new ObservableCollection<LogEntry>();

                    using (SqlConnection conn = FDMSDashboard.ServerConnector.GetConnection())
                    {
                        conn.Open();

                        string query = @"
                            SELECT TOP 1000 
                                Timestamp, 
                                Level, 
                                Source, 
                                Message 
                            FROM SystemLogs 
                            ORDER BY Timestamp DESC";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    logList.Add(new LogEntry
                                    {
                                        Timestamp = reader.GetDateTime(0),
                                        Level = reader.GetString(1),
                                        Source = reader.GetString(2),
                                        Message = reader.GetString(3)
                                    });
                                }
                            }
                        }
                    }

                    return logList;
                });

                Dispatcher.Invoke(() =>
                {
                    logEntries.Clear();
                    foreach (var log in logs)
                    {
                        logEntries.Add(log);
                    }
                    ApplyFilters();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading logs: {ex.Message}",
                        "Database Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });

                isConnected = false;
            }
        }

		/// <summary>
		/// Applies the selected log level and search text filters to the in-memory log collection.
		/// </summary>
		private void ApplyFilters()
        {
            var searchText = TxtSearch.Text.ToLower();
            var selectedLevel = (CmbLogLevel.SelectedItem as ComboBoxItem)?.Content.ToString();

            var filtered = logEntries.AsEnumerable();

            if (selectedLevel != "ALL" && !string.IsNullOrEmpty(selectedLevel))
            {
                filtered = filtered.Where(log =>
                    log.Level.Equals(selectedLevel, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(searchText) && searchText != "search logs...")
            {
                filtered = filtered.Where(log =>
                    log.Message.ToLower().Contains(searchText) ||
                    log.Source.ToLower().Contains(searchText) ||
                    log.Level.ToLower().Contains(searchText));
            }

            filteredLogEntries.Clear();
            foreach (var log in filtered)
            {
                filteredLogEntries.Add(log);
            }
        }

		/// <summary>
		/// Handles changes to the search textbox and reapplies log filters in real time.
		/// </summary>
		/// <param name="sender">The source text box that triggered the event.</param>
		/// <param name="e">The event data associated with the text change.</param>
		private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

		/// <summary>
		/// Handles log-level selection changes and updates the visible log entries.
		/// </summary>
		/// <param name="sender">The combo box that triggered the event.</param>
		/// <param name="e">The event data describing the selection change.</param>
		private void CmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

		/// <summary>
		/// Handles the Refresh button click and forces a manual reload of system logs from the database.
		/// </summary>
		/// <param name="sender">The Refresh button that was clicked.</param>
		/// <param name="e">The routed event data for the click event.</param>
		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsFromDatabase();
        }

		/// <summary>
		/// Handles the Clear Logs button click and confirms whether the user wants to clear all logs.
		/// </summary>
		/// <param name="sender">The Clear Logs button that was clicked.</param>
		/// <param name="e">The routed event data for the click event.</param>
		private async void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all logs? This action cannot be undone.",
                "Confirm Clear Logs",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await ClearLogsFromDatabase();
            }
        }

		/// <summary>
		/// Archives current system logs, deletes them from the active table, and reloads the UI.
		/// </summary>
		/// <returns>A task that represents the asynchronous log clearing operation.</returns>
		private async Task ClearLogsFromDatabase()
        {
            try
            {
                await Task.Run(() =>
                {
                    using (SqlConnection conn = FDMSDashboard.ServerConnector.GetConnection())
                    {
                        conn.Open();

                        string archiveQuery = @"
                            INSERT INTO SystemLogsArchive 
                            SELECT *, GETDATE() as ArchivedDate 
                            FROM SystemLogs";

                        using (SqlCommand archiveCmd = new SqlCommand(archiveQuery, conn))
                        {
                            archiveCmd.ExecuteNonQuery();
                        }

                        // Clear current logs
                        string clearQuery = "DELETE FROM SystemLogs";
                        using (SqlCommand clearCmd = new SqlCommand(clearQuery, conn))
                        {
                            clearCmd.ExecuteNonQuery();
                        }
                    }
                });

                await LoadLogsFromDatabase();

                MessageBox.Show("Logs cleared successfully and archived.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing logs: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

		/// <summary>
		/// Writes an error entry to a local fallback text file when database logging is unavailable.
		/// </summary>
		/// <param name="source">A short identifier describing where the error occurred.</param>
		/// <param name="message">The detailed error message to record.</param>
		private void LogError(string source, string message)
        {
            try
            {
                System.IO.File.AppendAllText(
                    "local_error_log.txt",
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {source} | {message}\n");
            }
            catch
            {
                // Silently fail if local logging also fails
            }
        }

		/// <summary>
		/// Stops timers, opens the Search &amp; Query window, and closes the current logsPage instance.
		/// </summary>
		/// <param name="sender">The control that initiated the navigation.</param>
		/// <param name="e">The routed event data for the click event.</param>
		private void NavigateToSearchQuery(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop timers before navigation to prevent memory leaks
                refreshTimer?.Stop();
                connectionCheckTimer?.Stop();

                // Create and show Search & Query page (matching Dashboard naming)
                var searchPage = new SearchingPageApp(dashboard);
                searchPage.Show();

                // Close current window
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Restart timers if navigation failed
                refreshTimer?.Start();
                connectionCheckTimer?.Start();
            }
        }

		/// <summary>
		/// Stops timers, opens the user login window, and closes the current logsPage instance.
		/// </summary>
		/// <param name="sender">The control that initiated the navigation.</param>
		/// <param name="e">The routed event data for the click event.</param>
		private void NavigateToUser(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop timers before navigation
                refreshTimer?.Stop();
                connectionCheckTimer?.Stop();

                // Create and show User/Login page (matching Dashboard naming)
                var loginPage = new UsersLoginPage();
                loginPage.Show();

                // Close current window
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Restart timers if navigation failed
                refreshTimer?.Start();
                connectionCheckTimer?.Start();
            }
        }

		/// <summary>
		/// Performs generic navigation by stopping timers, showing the target window, and closing logsPage.
		/// </summary>
		/// <param name="targetPage">The destination window to display.</param>
		private void NavigateToPage(Window targetPage)
        {
            try
            {
                // Cleanup current resources
                refreshTimer?.Stop();
                connectionCheckTimer?.Stop();

                // Show new page
                targetPage.Show();

                // Close current page
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                LogError("Navigation", ex.Message);

                // Restart timers if navigation failed
                refreshTimer?.Start();
                connectionCheckTimer?.Start();
            }
        }

		/// <summary>
		/// Searches the logical UI tree for a button whose Content matches the specified text.
		/// </summary>
		/// <param name="content">The button content text to search for.</param>
		/// <returns>The first matching Button instance, or null if none is found.</returns>
		private Button FindButtonByContent(string content)
        {
            return LogicalTreeHelper.GetChildren(this)
                .OfType<Grid>()
                .SelectMany(g => LogicalTreeHelper.GetChildren(g).OfType<StackPanel>())
                .SelectMany(sp => LogicalTreeHelper.GetChildren(sp).OfType<Button>())
                .FirstOrDefault(b => b.Content?.ToString() == content);
        }

		/// <summary>
		/// Stops active timers before the window closes to avoid background activity and memory leaks.
		/// </summary>
		/// <param name="e">Event data that can cancel the closing operation.</param>
		protected override void OnClosing(CancelEventArgs e)
        {
            refreshTimer?.Stop();
            connectionCheckTimer?.Stop();
            base.OnClosing(e);
        }
    }

	/// <summary>
	/// Raises the PropertyChanged event for the given property to update WPF bindings.
	/// </summary>
	/// <param name="propertyName">The name of the property that changed.</param>
	public class LogEntry : INotifyPropertyChanged
    {
        private DateTime timestamp;
        private string level;
        private string source;
        private string message;

        public DateTime Timestamp
        {
            get => timestamp;
            set
            {
                timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
                OnPropertyChanged(nameof(FormattedTimestamp));
            }
        }

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        public string Level
        {
            get => level;
            set
            {
                level = value;
                OnPropertyChanged(nameof(Level));
            }
        }

        public string Source
        {
            get => source;
            set
            {
                source = value;
                OnPropertyChanged(nameof(Source));
            }
        }

        public string Message
        {
            get => message;
            set
            {
                message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}