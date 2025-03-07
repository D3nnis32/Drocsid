using Logic.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic.UI
{
    public class ViewModelLocator
    {
        public MainWindowViewModel MainWindowVM { get; set; }
        public ChatInterfaceUserControlViewModel ChatInterfaceUCVM { get; set; }
        public LoginUserControlViewModel LoginUCVM { get; set; }
        public ChatUserControlViewModel ChatUCVM { get; set; }
        public ViewModelLocator()
        {
            MainWindowVM = new MainWindowViewModel();
            LoginUCVM = new LoginUserControlViewModel(MainWindowVM);
            ChatInterfaceUCVM = new ChatInterfaceUserControlViewModel();
            ChatUCVM = new ChatUserControlViewModel();
        }
    }
}
