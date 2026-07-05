using System.Windows;
using mixer.ViewModels;

namespace mixer.Views
{
    public partial class EditCellWindow : Window
    {
        public EditCellWindow(EditCellWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            if (DataContext is EditCellWindowViewModel vm)
                vm.Cleanup();
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}