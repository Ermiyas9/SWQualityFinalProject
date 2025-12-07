/* ======================================================================================================================== */
/* FILE             : logsPage.xaml.cs                                                                                      */
/* PROJECT          : Software Quality Final Project Milestone 2                                                            */
/* NAMESPACE        : GroundTerminalApp                                                                                    */
/* PROGRAMMER       : Ermiyas (Endalkachew) Gulti, Mher Keshishian, Quang Minh Vu, Saje'- Antoine Rose                      */
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



#pragma warning disable IDE0044 // Make field readonly

namespace GroundTerminalApp
{
    public partial class LogsPage : Window
    {
        private const int AUTO_REFRESH_INTERVAL = 30; // seconds

        private ObservableCollection<LogEntry> logEntries;
        private ObservableCollection<LogEntry> filteredLogEntries;

        private DispatcherTimer refreshTimer;
        private DispatcherTimer connectionCheckTimer;

        private bool isConnected = false;
        // Reference to SearchingPageApp for navigation
        //private SearchingPageApp searchingPage;

        //SearchingPageApp searchingPageInstance;
        FDMSDashboard dashboard;

        /*
         Method: logsPage (Constructor)
         Description: Initializes logsPage window with data collections, event handlers, and timers
         Receives dashboard instance reference and establishes initial connection and log load
         Parameters: FDMSDashboard dashboardInstance
         Returns: void
         */
        public LogsPage(FDMSDashboard dashboardInstance)
        {
            InitializeComponent();
            InitializeData();
            SetupEventHandlers();
            InitializeTimers();

            CmbLogLevel.SelectedIndex = 0;

            //searchingPage = searchingPageInstance;
            dashboard = dashboardInstance;
        }

        /*
         Method: InitializeData
         Description: Creates observable collections and binds filtered logs to data grid
         Sets up data source for UI display with empty initial state
         Parameters: None
         Returns: void
         */
        private void InitializeData()
        {
            logEntries = new ObservableCollection<LogEntry>();
            filteredLogEntries = new ObservableCollection<LogEntry>();
            LogsDataGrid.ItemsSource = filteredLogEntries;

            //searchingPage = searchingPageInstance;
        }

        /*
         Method: SetupEventHandlers
         Description: Wires event handlers for search box, log level filter, and toolbar buttons
         Connects UI controls to filter and navigation event handlers
         Parameters: None
         Returns: void
         */
        private void SetupEventHandlers()
        {
            TxtSearch.TextChanged += TxtSearch_TextChanged;
            CmbLogLevel.SelectionChanged += CmbLogLevel_SelectionChanged;

            // Attach Refresh button handler
            var refreshButton = FindButtonByContent("Refresh");
            if (refreshButton != null)
                refreshButton.Click += RefreshButton_Click;

            // Attach Clear Logs button handler
            var clearButton = FindButtonByContent("Clear Logs");
            if (clearButton != null)
                clearButton.Click += ClearLogsButton_Click;

            // Attach Search & Query navigation handler
            var searchQueryBtn = FindButtonByContent("Search & Query");
            if (searchQueryBtn != null)
                searchQueryBtn.Click += NavigateToSearchQuery;

            // Attach User/Login navigation handler
            var userBtn = FindButtonByContent("User");
            if (userBtn != null)
                userBtn.Click += NavigateToUser;
        }

        /*
         Method: InitializeTimers
         Description: Creates and starts periodic timers for auto-refresh and connection monitoring
         Triggers initial database connection check and log load on startup
         Parameters: None
         Returns: void
         */
        private void InitializeTimers()
        {
            // Timer for automatic log refresh from database
            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AUTO_REFRESH_INTERVAL)
            };
            refreshTimer.Tick += async (s, e) => await LoadLogsFromDatabase();
            refreshTimer.Start();

            // Timer for periodic connection status verification
            connectionCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            connectionCheckTimer.Tick += async (s, e) => await CheckDatabaseConnection();
            connectionCheckTimer.Start();

            // Initial connection and log load on application startup
            Task.Run(async () =>
            {
                await CheckDatabaseConnection();
                await LoadLogsFromDatabase();
            });
        }

        /*
         Method: CheckDatabaseConnection
         Description: Tests SQL connection and updates UI status indicators
         Runs on background thread and invokes UI updates on dispatcher thread
         Parameters: None
         Returns: Task<bool> - true if connection successful, false otherwise
         */
        private async Task<bool> CheckDatabaseConnection()
        {
            try
            {
                // Attempt connection on background thread
                await Task.Run(() =>
                {
                    using (SqlConnection conn = FDMSDashboard.ServerConnector.GetConnection())
                    {
                        conn.Open();
                        isConnected = true;
                    }
                });

                // Update UI on dispatcher thread with online status
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

                // Update UI on dispatcher thread with offline status
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

        /*
         Method: LoadLogsFromDatabase
         Description: Queries SystemLogs table and populates observable collections with latest entries
         Retrieves top 1000 logs sorted by timestamp and applies current filters
         Parameters: None
         Returns: Task - async log loading operation
         */
        private async Task LoadLogsFromDatabase()
        {
            // Skip load if database not connected
            if (!isConnected)
            {
                await CheckDatabaseConnection();
                if (!isConnected) return;
            }

            try
            {
                // Execute database query on background thread
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
                                // Extract each log entry from query results
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

                // Update UI on dispatcher thread with loaded logs
                Dispatcher.Invoke(() =>
                {
                    logEntries.Clear();
                    foreach (var log in logs)
                    {
                        logEntries.Add(log);
                    }
                    // Reapply filters after loading new data
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

        /*
         Method: ApplyFilters
         Description: Filters log collection by selected level and search text
         Updates filtered collection to reflect current filter state in real-time
         Parameters: None
         Returns: void
         */
        private void ApplyFilters()
        {
            var searchText = TxtSearch.Text.ToLower();
            var selectedLevel = (CmbLogLevel.SelectedItem as ComboBoxItem)?.Content.ToString();

            var filtered = logEntries.AsEnumerable();

            // Filter by log level if not set to ALL
            if (selectedLevel != "ALL" && !string.IsNullOrEmpty(selectedLevel))
            {
                filtered = filtered.Where(log =>
                    log.Level.Equals(selectedLevel, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by search text if provided
            if (!string.IsNullOrWhiteSpace(searchText) && searchText != "search logs...")
            {
                filtered = filtered.Where(log =>
                    log.Message.ToLower().Contains(searchText) ||
                    log.Source.ToLower().Contains(searchText) ||
                    log.Level.ToLower().Contains(searchText));
            }

            // Update filtered collection for UI binding
            filteredLogEntries.Clear();
            foreach (var log in filtered)
            {
                filteredLogEntries.Add(log);
            }
        }

        /*
         Method: TxtSearch_TextChanged
         Description: Event handler for search text changes
         Reapplies filters whenever user modifies search input
         Parameters: object sender, TextChangedEventArgs e
         Returns: void
         */
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        /*
         Method: CmbLogLevel_SelectionChanged
         Description: Event handler for log level dropdown selection changes
         Reapplies filters to show only logs matching selected level
         Parameters: object sender, SelectionChangedEventArgs e
         Returns: void
         */
        private void CmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        /*
         Method: RefreshButton_Click
         Description: Event handler for manual refresh button click
         Forces immediate reload of logs from database
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsFromDatabase();
        }

        /*
         Method: ClearLogsButton_Click
         Description: Event handler for clear logs button click
         Prompts user for confirmation before clearing and archiving logs
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private async void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm destructive action with user
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

        /*
         Method: ClearLogsFromDatabase
         Description: Archives current logs and deletes them from active SystemLogs table
         Reloads UI after archive operation completes
         Parameters: None
         Returns: Task - async clearing operation
         */
        private async Task ClearLogsFromDatabase()
        {
            try
            {
                // Archive and delete on background thread
                await Task.Run(() =>
                {
                    using (SqlConnection conn = FDMSDashboard.ServerConnector.GetConnection())
                    {
                        conn.Open();

                        // Copy logs to archive table with current timestamp
                        string archiveQuery = @"
                            INSERT INTO SystemLogsArchive 
                            SELECT *, GETDATE() as ArchivedDate 
                            FROM SystemLogs";

                        using (SqlCommand archiveCmd = new SqlCommand(archiveQuery, conn))
                        {
                            archiveCmd.ExecuteNonQuery();
                        }

                        // Remove archived logs from active table
                        string clearQuery = "DELETE FROM SystemLogs";
                        using (SqlCommand clearCmd = new SqlCommand(clearQuery, conn))
                        {
                            clearCmd.ExecuteNonQuery();
                        }
                    }
                });

                // Reload UI to reflect cleared state
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

        /*
         Method: LogError
         Description: Writes error entries to fallback text file when database logging unavailable
         Used for critical errors preventing normal database operations
         Parameters: string source, string message
         Returns: void
         */
        private void LogError(string source, string message)
        {
            try
            {
                // Append error to local file with timestamp
                System.IO.File.AppendAllText(
                    "local_error_log.txt",
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {source} | {message}\n");
            }
            catch
            {
                // Silently fail if local logging also fails - no cascading failures
            }
        }

        /*
         Method: NavigateToSearchQuery
         Description: Stops timers, opens SearchingPageApp window, and closes logsPage
         Transfers dashboard instance to search page for data continuity
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private void NavigateToSearchQuery(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop active timers to prevent background resource usage
                refreshTimer?.Stop();
                connectionCheckTimer?.Stop();

                // Create new search page with dashboard reference
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

                // Restart timers if navigation fails
                refreshTimer?.Start();
                connectionCheckTimer?.Start();
            }
        }

        /*
         Method: NavigateToUser
         Description: Stops timers, opens UsersLoginPage window, and closes logsPage
         Returns user to login screen for authentication change or logout
         Parameters: object sender, RoutedEventArgs e
         Returns: void
         */
        private void NavigateToUser(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop active timers before navigation
                refreshTimer?.Stop();
                connectionCheckTimer?.Stop();

                // Create and show login page
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

                // Restart timers if navigation fails
                refreshTimer?.Start();
                connectionCheckTimer?.Start();
            }
        }

        /*
         Method: NavigateToPage
         Description: Generic navigation method that stops timers, shows target window, and closes logsPage
         Centralizes navigation cleanup and error handling logic
         Parameters: Window targetPage
         Returns: void
         */
        private void NavigateToPage(Window targetPage)
        {
            try
            {
                // Clean up resources before navigation
                refreshTimer?.Stop();
                connectionCheckTimer?.Stop();

                // Display destination window
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

                // Restart timers if navigation fails
                refreshTimer?.Start();
                connectionCheckTimer?.Start();
            }
        }

        /*
         Method: FindButtonByContent
         Description: Searches logical UI tree for button matching content text
         Used to locate dynamically created or nested buttons without direct references
         Parameters: string content
         Returns: Button - matching button or null if not found
         */
        private Button FindButtonByContent(string content)
        {
            return LogicalTreeHelper.GetChildren(this)
                .OfType<Grid>()
                .SelectMany(g => LogicalTreeHelper.GetChildren(g).OfType<StackPanel>())
                .SelectMany(sp => LogicalTreeHelper.GetChildren(sp).OfType<Button>())
                .FirstOrDefault(b => b.Content?.ToString() == content);
        }

        /*
         Method: OnClosing
         Description: Override called when window is closing
         Stops active timers to prevent background operations and memory leaks
         Parameters: CancelEventArgs e
         Returns: void
         */
        protected override void OnClosing(CancelEventArgs e)
        {
            // Stop timers to release background resources
            refreshTimer?.Stop();
            connectionCheckTimer?.Stop();
            base.OnClosing(e);
        }
    }

    /*
     Class: LogEntry
     Description: Model for system log entry with automatic INotifyPropertyChanged binding
     Implements property change notifications for WPF UI updates on property modifications
     */
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

        // Formatted timestamp string for UI display
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

        /*
         Method: OnPropertyChanged
         Description: Raises PropertyChanged event to notify WPF bindings of property updates
         Enables automatic UI refresh when log entry properties are modified
         Parameters: string propertyName
         Returns: void
         */
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
