using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using static GroundTerminalApp.FDMSDashboard;


namespace GroundTerminalApp
{
    
    public partial class UsersLoginPage : Window
    {
        private System.Windows.Threading.DispatcherTimer loginSuccessTimer;
        private bool isAuthenticating;

        /*
        Method: UsersLoginPage() [Constructor]
        Description: Initializes login window
        */
        public UsersLoginPage()
        {
            InitializeComponent();
            isAuthenticating = false;
            CheckDatabaseConnection();
		}

        /*
        Method: BtnLogin_Click
        Description: Event handler for login button click
        Parses credentials and passes to team for database authentication
        Parameters: object sender, RoutedEventArgs e
        Returns: void
        */
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // Prevent multiple simultaneous authentication attempts
            if (isAuthenticating)
                return;

            isAuthenticating = true;
            BtnLogin.IsEnabled = false;

            // Parse credentials from UI
            LoginCredentials credentials = ParseLoginCredentials();

            // Validate parsed data is not empty
            if (!credentials.IsValid)
            {
                UpdateLoginStatus(false, "Username and password required.");
                ClearPasswordField();
                isAuthenticating = false;
                BtnLogin.IsEnabled = true;
                return;
            }

            // Pass parsed credentials for database authentication
            AuthenticationResult result = OnLoginAttempt(credentials);

            // Validate returned result
            if (result == null)
            {
                UpdateLoginStatus(false, "Authentication service unavailable.");
                ClearPasswordField();
                isAuthenticating = false;
                BtnLogin.IsEnabled = true;
                return;
            }

            // Display result from  database lookup
            UpdateLoginStatus(result.IsSuccess, result.Message);

            // If authentication successful, store user data and close
            if (result.IsSuccess && result.AuthenticatedUser != null)
            {
                CurrentUser = result.AuthenticatedUser;
                ScheduleWindowClose();
            }
            else
            {
                // Clear password on failed login for security
                ClearPasswordField();
                isAuthenticating = false;
                BtnLogin.IsEnabled = true;
            }
        }

        /*
        Method: ParseLoginCredentials
        Description: Extracts username and password from UI controls
        Trims whitespace and validates not empty
        Parameters: None
        Returns: LoginCredentials - Parsed username, password, and validity flag
        */
        private LoginCredentials ParseLoginCredentials()
        {
            // Extract and trim username from TextBox
            string username = TxtUsername.Text?.Trim() ?? string.Empty;

            // Extract and trim password from PasswordBox
            string password = TxtPassword.Password?.Trim() ?? string.Empty;

            // Return parsed credentials with validity flag
            return new LoginCredentials
            {
                Username = username,
                Password = password,
                IsValid = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
            };
        }

        /*
        Method: ClearPasswordField
        Description: Clears password from UI for security
        Removes password text from PasswordBox after login attempt
        Parameters: None
        Returns: void
        */
        private void ClearPasswordField()
        {
            TxtPassword.Clear();
        }

        /*
        Method: UpdateLoginStatus
        Description: Displays login result message to user
        Updates label text and color based on success/failure
        Parameters: bool isSuccess - True if login successful
                    string message - Status message to display
        Returns: void
        */
        private void UpdateLoginStatus(bool isSuccess, string message)
        {
            LoginStatusLbl.Text = message;
            LoginStatusLbl.Foreground = isSuccess ? Brushes.Green : Brushes.Red;
        }

        /*
        Method: ScheduleWindowClose
        Description: Schedules window close after delay to show success message
        Parameters: None
        Returns: void
        */
        private void ScheduleWindowClose()
        {
            loginSuccessTimer = new System.Windows.Threading.DispatcherTimer();
            loginSuccessTimer.Interval = TimeSpan.FromMilliseconds(1500);
            loginSuccessTimer.Tick += (s, args) =>
            {
                loginSuccessTimer.Stop();
                this.Close();
            };
            loginSuccessTimer.Start();
        }

        /*
        Method: Window_Closed
        Description: Event handler when login window closes
        Clears sensitive data and resources
        Parameters: object sender, EventArgs e
        Returns: void
        */
        private void Window_Closed(object sender, EventArgs e)
        {
            // Clear static user data for security
            CurrentUser = null;

            // Stop any running timers
            if (loginSuccessTimer != null && loginSuccessTimer.IsEnabled)
            {
                loginSuccessTimer.Stop();
            }

            // Clear password from UI
            TxtPassword.Clear();
        }

		// Authenticates credentials using AppUser table
		public AuthenticationResult OnLoginAttempt(LoginCredentials credentials)
		{
			var result = new AuthenticationResult
			{
				IsSuccess = false,
				Message = "Invalid username or password.",
				AuthenticatedUser = null
			};

			try
			{
                SqlConnection conn = ServerConnector.GetConnection();
				{
					conn.Open();

					string query = @"
                        SELECT UserId, Username, [Password], RoleId, IsActive
                        FROM dbo.AppUser
                        WHERE Username = @Username AND [Password] = @Password;";

                    SqlCommand cmd = new SqlCommand(query, conn);
					{
						cmd.Parameters.AddWithValue("@Username", credentials.Username);
						cmd.Parameters.AddWithValue("@Password", credentials.Password);

                        SqlDataReader reader = cmd.ExecuteReader();
						{
							if (reader.Read())
							{
								bool isActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
								if (!isActive)
								{
									result.IsSuccess = false;
									result.Message = "Account is disabled.";
									result.AuthenticatedUser = null;

									WriteSystemLog("ERROR", "UsersLoginPage", "Login failed for disabled account '" + credentials.Username + "'.");

									return result;
								}

								var user = new AppUser
								{
									UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
									Username = reader.GetString(reader.GetOrdinal("Username")),
									RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
									IsActive = isActive
								};

								result.IsSuccess = true;
								result.Message = "Login successful.";
								result.AuthenticatedUser = user;

								WriteSystemLog("INFO", "UsersLoginPage", "User '" + credentials.Username + "' logged in successfully.");

								return result;
							}
						}
					}
				}
			}
			catch (Exception)
			{
				result.IsSuccess = false;
				result.Message = "Login failed due to database error.";
				result.AuthenticatedUser = null;

				WriteSystemLog("ERROR", "UsersLoginPage", "Login failed due to database error.");

				return result;
			}

			WriteSystemLog("WARN", "UsersLoginPage", "Invalid login attempt for username '" + credentials.Username + "'.");
			return result;
		}


		/*
        Static Property: CurrentUser
        Description: Stores authenticated user data for application access
        Team members can retrieve user ID and role for authorization
        Usage: CurrentUser?.UserId, CurrentUser?.RoleId
        Cleared on window close for security
        */
		public static AppUser CurrentUser { get; set; }

		// Inserts one log row into SystemLogs table
		private void WriteSystemLog(string level, string source, string message)
		{
			try
			{
                SqlConnection conn = ServerConnector.GetConnection();
				{
					conn.Open();

					string sql = @"
                INSERT INTO dbo.SystemLogs ([Timestamp], [Level], [Source], [Message])
                VALUES (@Timestamp, @Level, @Source, @Message);";

                    SqlCommand cmd = new SqlCommand(sql, conn);
					{
						cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
						cmd.Parameters.AddWithValue("@Level", level);
						cmd.Parameters.AddWithValue("@Source", source);
						cmd.Parameters.AddWithValue("@Message", message);

						cmd.ExecuteNonQuery();
					}
				}
			}
			catch (Exception ex)
			{
				try
				{
					File.AppendAllText(
						"local_error_log.txt",
						DateTime.Now.ToString("s") + " [LoginLogError] " + ex.Message + Environment.NewLine);
				}
				catch
				{
				}
			}
		}

		// Updates DB status label and icons
		private void UpdateDbConnectionStatus(bool isConnected)
		{
			if (isConnected)
			{
				dbConnectionStatusLbl.Foreground = Brushes.Green;
				dbConnectionStatusLbl.Content = "ONLINE";
				dbOnlineIcon.Visibility = Visibility.Visible;
				dbOfflineIcon.Visibility = Visibility.Collapsed;
			}
			else
			{
				dbConnectionStatusLbl.Foreground = Brushes.Red;
				dbConnectionStatusLbl.Content = "OFFLINE";
				dbOfflineIcon.Visibility = Visibility.Visible;
				dbOnlineIcon.Visibility = Visibility.Collapsed;
			}
		}

		// Tests DB connection and updates status controls
		private bool CheckDatabaseConnection()
		{
			try
			{
                SqlConnection conn = ServerConnector.GetConnection();
				{
					conn.Open();
					UpdateDbConnectionStatus(true);
					return true;
				}
			}
			catch
			{
				UpdateDbConnectionStatus(false);
				return false;
			}
		}
	}

	/*
    Class: LoginCredentials
    Description: Data transfer object containing parsed login credentials
    */
	public class LoginCredentials
    {
        public string Username { get; set; }             // Parsed from TxtUsername
        public string Password { get; set; }             // Parsed from TxtPassword
        public bool IsValid { get; set; }                // True if both not empty
    }

    /*
    Class: AuthenticationResult
    Description: Data transfer object returned from database lookup
    Contains success status, message, and authenticated user data
    */
    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }              // True if credentials valid
        public string Message { get; set; }              // Status message for display
        public AppUser AuthenticatedUser { get; set; }   // User data if authenticated
    }

    /*
    Class: AppUser
    Description: Data model for authenticated user from database
    */
    public class AppUser
    {
        public int UserId { get; set; }                  // User_ID from AppUser table
        public string Username { get; set; }             // User_name from AppUser table
        public int RoleId { get; set; }                  // appUserRole_ID for authorization
        public bool IsActive { get; set; }               // Is_Active status from database
    }
}

