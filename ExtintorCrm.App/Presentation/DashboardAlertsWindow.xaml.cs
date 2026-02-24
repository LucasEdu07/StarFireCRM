using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation
{
    public partial class DashboardAlertsWindow : Window
    {
        public DashboardAlertItem? SelectedAlert => AlertsGrid.SelectedItem as DashboardAlertItem;

        public DashboardAlertsWindow(string title, string subtitle, IEnumerable<DashboardAlertItem> items)
        {
            InitializeComponent();
            WindowTitleText.Text = title;
            TitleText.Text = title;
            SubtitleText.Text = subtitle;
            AlertsGrid.ItemsSource = items.ToList();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OpenProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAlert == null)
            {
                return;
            }

            DialogResult = true;
            Close();
        }

        private void AlertsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedAlert == null)
            {
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
