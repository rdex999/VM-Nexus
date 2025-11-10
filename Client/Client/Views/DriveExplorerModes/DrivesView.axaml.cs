using Avalonia.Controls;
using Avalonia.Input;
using Client.ViewModels.DriveExplorerModes;

namespace Client.Views.DriveExplorerModes;

public partial class DrivesView : UserControl
{
	public DrivesView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Handles a double click event on a drive. Informs the drive's item template of the event.
	/// </summary>
	/// <param name="sender">The control that has sent the event. sender != null &amp;&amp; sender is Border.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: User has double-clicked on a drive. sender != null &amp;&amp; sender is Border. <br/>
	/// Postcondition: The drive's item template is informed of the event.
	/// </remarks>
	private void OnDriveDoubleTapped(object? sender, TappedEventArgs e)
	{
		if (sender == null || sender is not Border border)
			return;

		if (border.DataContext is not DriveItemTemplate drive)
			return;

		drive.Open();
	}
}