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

	/// <summary>
	/// Handles a change in the operating system input field. (Calls the view model handler for this event.)
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: The user has changed the value of the operating system input field. <br/>
	/// Postcondition: The changed is handled and the VM creation settings change accordingly.
	/// </remarks>
	private async void OperatingSystemChangedAsync(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is CreateVmViewModel vm)
		{
			vm.OperatingSystemChanged();
			await vm.VmCreationInfoChangedAsync();
		}
	}
	
	/// <summary>
	/// Handles a change in one or more of the VM creation settings input field. (Calls the view model handler for this event.)
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: The user has changed one or more of the VM's creation settings input fields. <br/>
	/// Postcondition: The changed is handled and the VM creation settings change accordingly.
	/// </remarks>
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
			/*
			 * OsDriveSize is not automatically updated if the field is empty - but e.NewValue will contain null.
			 * So update OsDriveSize to be null when the field is empty
			 */
			vm.OsDriveSize = (int?)e.NewValue;	
		}
		VmCreationInfoChangedAsync(sender, e);
	}
	private void VmCreationInfoChangedComboBox(object? sender, SelectionChangedEventArgs e) => VmCreationInfoChangedAsync(sender, e);
}