using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Server.ViewModels;

namespace Server.Views;

public partial class UsersView : UserControl
{
	public UsersView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Handles the that the user deletion popup is closing. Closes the popup.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: The user deletion popup is closing. <br/>
	/// Postcondition: The user deletion popup is closed.
	/// </remarks>
	private void OnUserDeletePopupClosed(object? sender, EventArgs e)
	{
		if (DataContext is UsersViewModel vm)
			vm.CloseUserDeletePopup();
	}
}