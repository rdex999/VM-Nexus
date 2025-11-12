using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Client.ViewModels.DriveExplorerModes;

namespace Client.Views.DriveExplorerModes;

public partial class FileSystemItemsView : UserControl
{
	public FileSystemItemsView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Called when this view is shown - meaning DataContext is set and can be used.
	/// </summary>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: This view is attached to the visual tree - meaning its visible. <br/>
	/// Postcondition: The icons of files and directories (filesystem items) are set.
	/// </remarks>
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		
		UpdateFileSystemItemsIcons();
	}

	/// <summary>
	/// Updates the icons of all filesystem items. (files and directories)
	/// </summary>
	/// <remarks>
	/// Precondition: View attached to visual tree, updating filesystem item icons is required. <br/>
	/// Postcondition: The icons of all filesystem items (files and directories) are updated.
	/// </remarks>
	private void UpdateFileSystemItemsIcons()
	{
		if (DataContext is not FileSystemItemsViewModel vm)
			return;

		foreach (FileSystemItemItemTemplate item in vm.Items)
		{
			string key;
			if (item.IsFile)
			{
				string extension = item.Name.Split('.').Last().ToLower();
				key = extension switch
				{
					"txt" => "DocumentOnePageRegular",
					"pdf" => "DocumentPdfRegular",
					"png" or "jpeg" or "jpg" => "ImageRegular",
					_ => "DocumentRegular"
				};
			}
			else
				key = "FolderRegular";

			if (this.TryFindResource(key, out object? resource) && resource is Geometry geometry)
				item.Icon = geometry;
		}
	}
}