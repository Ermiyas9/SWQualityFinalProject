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

        /*
        Method: logsPage (Constructor)
        Description: Initializes the log viewer window, prepares data collections,
             wires UI event handlers, and starts background timers for
             log auto-refresh and database connection monitoring.
        */

        public logsPage()
        {
            InitializeComponent();
            InitializeData();
            SetupEventHandlers();
            InitializeTimers();

            CmbLogLevel.SelectedIndex = 0;
        }
        /*
        Method: InitializeData
        Description: Creates observable collections that store log entries and their
                     filtered view. Binds the DataGrid to the filtered collection.
        Purpose: Prepares in-memory data sources for UI binding.
        */

        private void InitializeData()
        {
            logEntries = new ObservableCollection<LogEntry>();
            filteredLogEntries = new ObservableCollection<LogEntry>();
            LogsDataGrid.ItemsSource = filteredLogEntries;
        }
        /*
        Method: SetupEventHandlers
        Description: Attaches UI event handlers for search, log-level filtering, and
                     button actions. Dynamically locates buttons by content to avoid
                     XAML coupling.
        Purpose: Ensures interactive UI components trigger logic correctly.
        */

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
        /*
        Method: InitializeTimers
        Description: Creates and starts DispatcherTimers for periodic log refreshing
                     and database health checks. Also triggers an immediate initial load.
        Purpose: Enables automatic UI updates and real-time DB connection monitoring.
        */

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

        /*
        Method: CheckDatabaseConnection
        Description: Verifies connectivity to the FDMS SQL database by opening a test
                     connection. Updates UI indicators and logs local errors on failure.
        Returns: bool - true if connection succeeded, otherwise false.
        Purpose: Maintains real-time connection status for user visibility.
        */

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

        /*
        Method: LoadLogsFromDatabase
        Description: Retrieves the latest 1000 log records from the SystemLogs table,
                     rebuilds the internal log collection, and reapplies UI filters.
        Async: Runs database operations on a background thread and marshals results
               back to the UI.
        Purpose: Populates and refreshes log data displayed to the user.
        */

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


        /*
        Method: ApplyFilters
        Description: Applies UI-selected log level and text search filters to the
                     in-memory log collection and updates the visible filtered list.
        Purpose: Provides fast client-side filtering without extra DB calls.
        */

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

        /*
        Method: TxtSearch_TextChanged
        Description: Triggers log filtering whenever the search textbox content changes.
        Purpose: Enables live search functionality.
        */

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        /*
        Method: CmbLogLevel_SelectionChanged
        Description: Reapplies filters when user changes the selected log level.
        Purpose: Dynamically updates the displayed log severity.
        */
        private void CmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        /*
        Method: RefreshButton_Click
        Description: Manually triggers a log reload from the database.
        Purpose: Allows user to force refresh outside the auto-refresh interval.
        */
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsFromDatabase();
        }

        /*
        Method: ClearLogsButton_Click
        Description: Displays a confirmation dialog and initiates log clearing when 
                     the user approves.
        Purpose: Protects against accidental deletion of log history.
        */
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

        /*
        Method: ClearLogsFromDatabase
        Description: Archives all current logs into SystemLogsArchive, deletes them
                     from SystemLogs, and then reloads UI data.
        Purpose: Performs safe log cleanup while preserving long-term history.
        */
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

        /*
        Method: LogError
        Description: Writes errors to a local text file fallback logger.
        Purpose: Provides diagnostic logging when DB logging is unavailable.
        */
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

        /*
        Method: NavigateToSearchQuery
        Description: Stops timers, navigates to the Search & Query page, and closes
                     the current window. Restarts timers if navigation fails.
        Purpose: Ensures clean window transitions without leaked timers.
        */
        private void NavigateToSearchQuery(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop timers before navigation to prevent memory leaks
                refreshTimer?.Stop();
                connectionCheckTimer?.Stop();

                // Create and show Search & Query page (matching Dashboard naming)
                var searchPage = new SearchingPageApp();
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

        /*
        Method: NavigateToUser
        Description: Navigates to the User/Login page while performing cleanup of
                     active timers. Handles navigation exceptions safely.
        Purpose: Supports page transitions within the Ground Terminal UI.
        */
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

        /*
        Method: NavigateToPage
        Description: Generic navigation helper that stops timers, opens a target window,
                     and closes the current one. Includes error fallback behavior.
        Purpose: Centralizes window transition logic to reduce repetition.
        */
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

        /*
        Method: FindButtonByContent
        Description: Searches the logical UI tree for a Button with the specified content.
        Returns: Button - first match or null.
        Purpose: Decouples button lookup from XAML structure; supports dynamic layouts.
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
        Description: Stops active timers before the window closes to prevent memory leaks
                     and orphaned background threads.
        Purpose: Ensures proper resource cleanup on exit.
        */
        protected override void OnClosing(CancelEventArgs e)
        {
            refreshTimer?.Stop();
            connectionCheckTimer?.Stop();
            base.OnClosing(e);
        }
    }

    /*
    Class: LogEntry
    Description: Data model representing a single log record. Implements
                 INotifyPropertyChanged for WPF data binding.
    Purpose: Used as the row data structure in the Logs DataGrid.
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