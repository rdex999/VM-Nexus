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

	/// <summary>
	/// Handles a double tap on the path bar. Switches the path bar into text mode.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: User has double-tapped the path bar. <br/>
	/// Postcondition: Path bar is switched into text mode.
	/// </remarks>
	private void OnPathBarDoubleTapped(object? sender, TappedEventArgs e)
	{
		if (DataContext is DriveExplorerViewModel vm)
			vm.ChangeIntoTextPathBar();
	}
}