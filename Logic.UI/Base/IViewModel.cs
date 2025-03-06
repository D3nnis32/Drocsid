using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic.UI.Base
{
    public interface IViewModel<TypeOfModel> : INotifyPropertyChanged
    {
        TypeOfModel Model { get; set; }

        void NewModelAssigned();
    }
}