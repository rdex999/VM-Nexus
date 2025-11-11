using Avalonia.Controls;
using Avalonia.Input;
using Client.ViewModels.DriveExplorerModes;

namespace Client.Views.DriveExplorerModes;

public partial class PartitionsView : UserControl
{
	public PartitionsView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Handles a double click on a partition. Informs the partition's item template, which in turn attempts to open the partition.
	/// </summary>
	/// <param name="sender">The control that was double tapped. sender != null &amp;&amp; sender is Border.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: A partition was double tapped. sender != null &amp;&amp; sender is Border. <br/>
	/// Postcondition: The partition's item template is informed of the event, and will attempt to open the partition.
	/// </remarks>
	private void OnPartitionDoubleTapped(object? sender, TappedEventArgs e)
	{
		if (sender == null || sender is not Border border)
			return;

		if (border.DataContext is not PartitionItemTemplate partition)
			return;

		partition.Open();
	}
}