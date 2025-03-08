using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Logic.UI.ViewModels
{
    public class AddChannelWindowViewModel
    {
        public RelayCommand CreateChannelCommand { get; set; }
        AddChannelWindowViewModel() 
        {
            CreateChannelCommand = new RelayCommand(() => CreateChannel());
        }

        public void CreateChannel()
        {
            var addChannelWindow = new AddChannelWindow
            {
                Data
            }
        }
    }
}
