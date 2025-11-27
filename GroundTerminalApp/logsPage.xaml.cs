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
    /// Interaction logic for logsPage.xaml
    /// </summary>
    public partial class logsPage : Window
    {
        public logsPage()
        {
            InitializeComponent();

            // so I can call that I created in another window, since that method takes label check box and bool variable i will pass those 
            var searchPage = new SearchingPageApp();
            searchPage.UpdateConnectionStatus(connectionStatusLbl, connStatChkBx, true);
        }
    }
}
