using System.Windows.Controls;
using System.Windows.Input;
using BatteryTabVision.Core.Models;
using BatteryTabVision.App.ViewModels;

namespace BatteryTabVision.App.Views;

public partial class InspectionHistoryView : UserControl
{
    public InspectionHistoryView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is InspectionRecord record)
        {
            var vm = DataContext as InspectionHistoryViewModel;
            vm?.ShowDetailCommand.Execute(record);
        }
    }
}
