using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Client.ViewModels;

namespace Client.Views;

public partial class VmScreenView : UserControl
{
	private Size _vmScreenSize;			/* Current size in DIP */
	private Size _vmFramebufferSize;	/* Current size in pixels (for example, 1920x1080) */
	
	public VmScreenView()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		if(DataContext is not VmScreenViewModel vm) return;

		vm.NewFrameReceived += () => VmScreenImage.InvalidateVisual();
		vm.VmFramebufferSizeChanged += (int width, int height) => _vmFramebufferSize = new Size(width, height);
	}

	/// <summary>
	/// Convert a mouse position relative to the screen image in DIP, into a mouse position in pixels.
	/// </summary>
	/// <param name="x">The X component of the DIP mouse position. must be in valid range.</param>
	/// <param name="y">The Y component of the DIP mouse position. must be in valid range.</param>
	/// <param name="pixelX">The outputted X component of the pixel mouse position.</param>
	/// <param name="pixelY">The outputted Y component of the pixel mouse position.</param>
	/// <remarks>
	/// Precondition: A conversion of a DIP mouse position to pixel mouse position is needed. x and y must be in valid range. <br/>
	/// Postcondition: The X and Y pixel positions are written to pixelX and pixelY respectively.
	/// </remarks>
	private void PointerPositionToPixels(double x, double y, out int pixelX, out int pixelY)
	{
		pixelX = (int)Math.Round(_vmFramebufferSize.Width * (x / _vmScreenSize.Width));
		pixelY = (int)Math.Round(_vmFramebufferSize.Height * (y / _vmScreenSize.Height));
	}

	/// <summary>
	/// Use a pointer event to convert the mouse position relative to the screen image in DIP, into a mouse position in pixels.
	/// </summary>
	/// <param name="sender">The control that sent the pointer event. sender != null.</param>
	/// <param name="e">The pointer event arguments. e != null</param>
	/// <param name="pixelX">The outputted X component of the pixel mouse position.</param>
	/// <param name="pixelY">The outputted Y component of the pixel mouse position.</param>
	/// <remarks>
	/// Precondition: A conversion of a DIP mouse position to pixel mouse position is needed. sender != null &amp;&amp; e != null. <br/>
	/// Postcondition: The X and Y pixel positions are written to pixelX and pixelY respectively.
	/// </remarks>
	private void PointerPositionToPixels(object? sender, PointerEventArgs e, out int pixelX, out int pixelY)
	{
		PointerPoint pointerPoint = e.GetCurrentPoint(sender as Control);
		PointerPositionToPixels(pointerPoint.Position.X, pointerPoint.Position.Y, out pixelX, out pixelY);
	}

	/// <summary>
	/// Handles a change in the virtual machine screen image size. (change in DIP)
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="e">Event arguments for this event. e != null.</param>
	/// <remarks>
	/// Precondition: The size of the virtual machine screen image has changed. e != null. <br/>
	/// Postcondition: Event is handled. Size is updated.
	/// </remarks>
	private void OnVmScreenSizeChanged(object? sender, SizeChangedEventArgs e) => _vmScreenSize = e.NewSize;

	/// <summary>
	/// Handles a mouse movement.
	/// </summary>
	/// <param name="sender">The control that the mouse has moved upon. sender != null.</param>
	/// <param name="e">The event arguments for this event. e != null.</param>
	/// <remarks>
	/// Precondition: The mouse has moved over some control. sender != null &amp;&amp; e != null. <br/>
	/// Postcondition: The event is handled. The view model is informed.
	/// </remarks>
	private void OnPointerMoved(object? sender, PointerEventArgs e)
	{
		if(DataContext is not VmScreenViewModel vm) return;

		PointerPositionToPixels(sender, e, out int x, out int y);
		vm.OnVmScreenPointerMoved(x, y);
	}

	private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		throw new NotImplementedException();
	}

	private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		throw new NotImplementedException();
	}

	private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		throw new NotImplementedException();
	}
}