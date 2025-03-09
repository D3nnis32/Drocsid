using System.Windows;

namespace Services


{
    public interface INavigationService
    {
        void OpenWindow<T>() where T : Window, new();
    }

}