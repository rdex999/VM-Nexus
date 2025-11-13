using Avalonia.Controls;
using Avalonia.Input;
using Client.ViewModels;

namespace Client.Views;

public partial class DriveExplorerView : UserControl
{
	public DriveExplorerView()
	{
		InitializeComponent();
	}

	private void OnPathBarDoubleTapped(object? sender, TappedEventArgs e)
	{
		if (DataContext is DriveExplorerViewModel vm)
			vm.ChangeIntoTextPathBar();
	}
}