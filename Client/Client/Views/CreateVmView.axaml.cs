using System;
using System.Threading.Tasks;
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

	private async void OperatingSystemChangedAsync(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is CreateVmViewModel vm)
		{
			vm.OperatingSystemChanged();
			await vm.VmCreationInfoChangedAsync();
		}
	}

	private async void VmCreationInfoChangedAsync(object? sender, EventArgs e)
	{
		if (DataContext is CreateVmViewModel vm)
		{
			await vm.VmCreationInfoChangedAsync();
		}	
	}

	private void VmCreationInfoChangedText(object? sender, TextChangedEventArgs e) => VmCreationInfoChangedAsync(sender, e);

	private void VmCreationInfoChangedNumeric(object? sender, NumericUpDownValueChangedEventArgs e)
	{
		if (DataContext is CreateVmViewModel vm)
		{
			vm.OsDriveSize = (int?)e.NewValue;
		}
		VmCreationInfoChangedAsync(sender, e);
	}
	private void VmCreationInfoChangedComboBox(object? sender, SelectionChangedEventArgs e) => VmCreationInfoChangedAsync(sender, e);
}