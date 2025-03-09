using Logic.UI.ViewModels;
using System.Windows;

namespace UI
{
    public partial class AddChannelWindow : Window
    {
        public AddChannelWindow()
        {
            InitializeComponent();

            // Subscribe to the view model's close request
            Loaded += (s, e) =>
            {
                if (DataContext is AddChannelWindowViewModel viewModel)
                {
                    viewModel.RequestClose += (sender, args) => Close();
                }
            };
        }
    }
}