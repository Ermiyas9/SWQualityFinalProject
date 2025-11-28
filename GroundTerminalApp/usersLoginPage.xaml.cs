using System;
using System.Collections.Generic;
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

namespace GroundTerminalApp
{
    /// <summary>
    /// Interaction logic for usersLoginPage.xaml
    /// </summary>
    public partial class UsersLoginPage : Window
    {
        public UsersLoginPage()
        {
            InitializeComponent();

            // so I can call that I created in another window, since that method takes label 
            // i made a little change here instead of using the checkbox i got icon from icons8.com website that we can user their icons 
            // so i am passing the images as a parameter
            var searchPage = new SearchingPageApp();
            bool connected = searchPage.ConnectToDatabase();

            // pass the controls as parameters using the real connection state so it gets offline when its offline 
            searchPage.UpdateConnectionStatus(dbConnectionStatusLbl, dbOnlineIcon, dbOfflineIcon, connected);
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text;
            string password = TxtPassword.Password;

            if (username == "admin" && password == "1234")
            {
                LoginStatusLbl.Text = "Login successful!";
                LoginStatusLbl.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                LoginStatusLbl.Text = "Invalid username or password.";
                LoginStatusLbl.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
    }
}
