using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Client.ViewModels;

namespace Client.Views;

public partial class CreateVmView : UserControl
{
	public CreateVmView()
	{
		InitializeComponent();
	}

	private void OperatingSystemChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is CreateVmViewModel vm)
		{
			vm.OperatingSystemChanged();
		}
	}
}