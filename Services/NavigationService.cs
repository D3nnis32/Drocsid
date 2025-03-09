using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Services
{
    public class NavigationService : INavigationService
    {
        public void OpenWindow<T>() where T : Window, new()
        {
            var window = new T();
            window.Show();
        }
    }
}
