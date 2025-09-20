using Avalonia;
using Avalonia.Controls;
using Client.ViewModels;

namespace Client.Views;

public partial class VmScreenView : UserControl
{
	public VmScreenView()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		if(DataContext is not VmScreenViewModel vm) return;

		vm.NewFrameReceived += () => VmScreenImage.InvalidateVisual();
	}
}