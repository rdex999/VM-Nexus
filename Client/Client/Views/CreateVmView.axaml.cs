using System;
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
			vm.VmCreationInfoChanged();
		}
	}

	private void VmCreationInfoChanged(object? sender, EventArgs e)
	{
		if (DataContext is CreateVmViewModel vm)
		{
			vm.VmCreationInfoChanged();
		}	
	}

	private void VmCreationInfoChangedText(object? sender, TextChangedEventArgs e) => VmCreationInfoChanged(sender, e);

	private void VmCreationInfoChangedNumeric(object? sender, NumericUpDownValueChangedEventArgs e)
	{
		if (DataContext is CreateVmViewModel vm)
		{
			vm.OsDriveSize = (int)e.NewValue!;
		}
		VmCreationInfoChanged(sender, e);
	}
	private void VmCreationInfoChangedComboBox(object? sender, SelectionChangedEventArgs e) => VmCreationInfoChanged(sender, e);
}