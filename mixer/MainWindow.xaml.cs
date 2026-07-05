using System.Windows;
using mixer.ViewModels;

namespace mixer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(App.WaveLinkService, App.MidiService, App.MidiMappingStorage);
        }
    }
}