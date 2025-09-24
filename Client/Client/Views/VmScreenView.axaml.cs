using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Client.ViewModels;
using Shared;
using Point = System.Drawing.Point;
using Size = Avalonia.Size;

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
	/// <returns>The pixel mouse position.</returns>
	/// <remarks>
	/// Precondition: A conversion of a DIP mouse position to pixel mouse position is needed. x and y must be in valid range. <br/>
	/// Postcondition: The X and Y pixel positions are written to pixelX and pixelY respectively.
	/// </remarks>
	private Point PointerPositionToPixels(double x, double y)
	{
		int pixelX = (int)Math.Round(_vmFramebufferSize.Width * (x / _vmScreenSize.Width));
		int pixelY = (int)Math.Round(_vmFramebufferSize.Height * (y / _vmScreenSize.Height));
		
		return new Point(pixelX, pixelY);
	}

	/// <summary>
	/// Use a pointer event to convert the mouse position relative to the screen image in DIP, into a mouse position in pixels.
	/// </summary>
	/// <param name="sender">The control that sent the pointer event. sender != null.</param>
	/// <param name="e">The pointer event arguments. e != null</param>
	/// <returns>The pixel mouse position.</returns>
	/// <remarks>
	/// Precondition: A conversion of a DIP mouse position to pixel mouse position is needed. sender != null &amp;&amp; e != null. <br/>
	/// Postcondition: The X and Y pixel positions are written to pixelX and pixelY respectively.
	/// </remarks>
	private Point PointerPositionToPixels(object? sender, PointerEventArgs e)
	{
		PointerPoint pointerPoint = e.GetCurrentPoint(sender as Control);
		return PointerPositionToPixels(pointerPoint.Position.X, pointerPoint.Position.Y);
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

		Point position = PointerPositionToPixels(sender, e);
		vm.OnVmScreenPointerMoved(position);
	}

	/// <summary>
	/// Get pressed pointer button flags from a pointer event.
	/// </summary>
	/// <param name="e">The pointer event arguments. Contains information on which buttons are pressed. e != null.</param>
	/// <returns>Flags indicating which pointer buttons are currently pressed. See SharedDefinitions.MouseButtons.</returns>
	/// <remarks>
	/// Precondition: e != null. <br/>
	/// Postcondition: An integer (flags) representing the currently pressed pointer buttons is returned.
	/// </remarks>
	private int PressedButtonsFromPointerEvent(PointerEventArgs e)
	{
		int pressed = (int)SharedDefinitions.MouseButtons.None;
		pressed |= e.Properties.IsLeftButtonPressed		? (int)SharedDefinitions.MouseButtons.Left		: 0;
		pressed |= e.Properties.IsRightButtonPressed	? (int)SharedDefinitions.MouseButtons.Right		: 0;
		pressed |= e.Properties.IsMiddleButtonPressed	? (int)SharedDefinitions.MouseButtons.Middle	: 0;	
		
		return pressed;
	}
	
	/// <summary>
	/// Handles a pointer button press/release.
	/// </summary>
	/// <param name="sender">The control that the mouse was clicked/released upon. sender != null.</param>
	/// <param name="e">The event arguments for this event. e != null.</param>
	/// <returns>
	/// Precondition: One or more of the pointers buttons have been pressed or released. sender != null &amp;&amp; e != null. <br/>
	/// Postcondition: The event is handled. The view model is informed.
	/// </returns>
	private void OnPointerButtonEvent(object? sender, PointerEventArgs e)
	{
		if(DataContext is not VmScreenViewModel vm) return;
		
		Point position = PointerPositionToPixels(sender, e);
		int pressed = PressedButtonsFromPointerEvent(e);
		
		vm.OnVmScreenPointerButtonEvent(position, pressed);
	}

	/// <summary>
	/// Handles a pointer button press.
	/// </summary>
	/// <param name="sender">The control that the mouse was clicked upon. sender != null.</param>
	/// <param name="e">The event arguments for this event. e != null.</param>
	/// <returns>
	/// Precondition: One or more of the pointers buttons have been pressed. sender != null &amp;&amp; e != null. <br/>
	/// Postcondition: The event is handled. The view model is informed.
	/// </returns>
	private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => OnPointerButtonEvent(sender, e);
	
	/// <summary>
	/// Handles a pointer button release.
	/// </summary>
	/// <param name="sender">The control that the mouse was released upon. sender != null.</param>
	/// <param name="e">The event arguments for this event. e != null.</param>
	/// <returns>
	/// Precondition: One or more of the pointers buttons have been released. sender != null &amp;&amp; e != null. <br/>
	/// Postcondition: The event is handled. The view model is informed.
	/// </returns>
	private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => OnPointerButtonEvent(sender, e);

	/// <summary>
	/// Handles a mouse wheel scroll.
	/// </summary>
	/// <param name="sender">The control that the mouse has scrolled upon. sender != null.</param>
	/// <param name="e">The event arguments for this event. e != null.</param>
	/// <remarks>
	/// Precondition: The mouse wheel has been scrolled. sender != null &amp;&amp; e != null. <br/>
	/// Postcondition: The event is handled, the view model is informed.
	/// </remarks>
	private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		if(DataContext is not VmScreenViewModel vm) return;
		
		Point position = PointerPositionToPixels(sender, e);
		int pressed = PressedButtonsFromPointerEvent(e);

		pressed |= e.Delta.Y > 0 ? (int)SharedDefinitions.MouseButtons.WheelUp		: 0;
		pressed |= e.Delta.Y < 0 ? (int)SharedDefinitions.MouseButtons.WheelDown	: 0;
		
		pressed |= e.Delta.X > 0 ? (int)SharedDefinitions.MouseButtons.WheelRight	: 0;
		pressed |= e.Delta.X < 0 ? (int)SharedDefinitions.MouseButtons.WheelLeft	: 0;
	
		vm.OnVmScreenPointerButtonEvent(position, pressed);
	}
}