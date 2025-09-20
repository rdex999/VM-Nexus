using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared;
using Shared.Networking;
using PixelFormat = Avalonia.Platform.PixelFormat;

namespace Client.ViewModels;

public partial class VmScreenViewModel : ViewModelBase
{
	public Action? NewFrameReceived;
	
	private bool _streamRunning = false;
	private SharedDefinitions.VmGeneralDescriptor? _vmDescriptor = null;
	private PixelFormat? _pixelFormat = null;

	[ObservableProperty] 
	private WriteableBitmap? _vmScreenBitmap = null;
	
	public VmScreenViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.VmScreenFrameReceived += OnVmScreenFrameReceived;
	}

	/// <summary>
	/// Runs when a new frame of a virtual machine is received.
	/// </summary>
	/// <param name="sender">Unused</param>
	/// <param name="frame">The frame that was received. frame != null.</param>
	/// <remarks>
	/// Precondition: A new frame of the screen of a virtual machine has been received. frame != null. <br/>
	/// Postcondition: The frame is rendered on the screen.
	/// </remarks>
	private void OnVmScreenFrameReceived(object? sender, MessageInfoVmScreenFrame frame)
	{
		if (!_streamRunning || _vmDescriptor == null || frame.VmId != _vmDescriptor.Id || _pixelFormat == null)
		{
			return;
		}

		if (VmScreenBitmap == null || VmScreenBitmap.PixelSize.Width * VmScreenBitmap.PixelSize.Height != frame.Size.Width * frame.Size.Height)
		{
			VmScreenBitmap = new WriteableBitmap(new PixelSize(frame.Size.Width, frame.Size.Height), new Vector(96, 96), _pixelFormat);
		}

		using ILockedFramebuffer buffer = VmScreenBitmap.Lock();
		Marshal.Copy(frame.Framebuffer, 0, buffer.Address, frame.Size.Width * frame.Size.Height * (_pixelFormat.Value.BitsPerPixel / 8));
		Dispatcher.UIThread.Invoke(NewFrameReceived!);
	}

	/// <summary>
	/// Resumes the screen stream.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The user has clicked on the screen page, (VM screen) or selected another tab. <br/>
	/// Postcondition: If there is a virtual machine configured, (_vmDescriptor != null) then the stream is resumed. If there was non, no action is taken.
	/// </remarks>
	public async Task<ExitCode> FocusAsync()
	{
		if (!_streamRunning && _vmDescriptor != null)
		{
			return await StartStreamAsync();
		}
		return ExitCode.Success;
	}

	/// <summary>
	/// Stops the screen stream. (To save bandwidth)
	/// </summary>
	/// <remarks>
	/// Precondition: User has quit the screen page. (selected another side menu page) <br/>
	/// Postcondition: If there was a stream running, it is stopped. If there was not, no action is taken.
	/// </remarks>
	public async Task UnfocusAsync()
	{
		if (_streamRunning)
		{
			await EndStreamAsync();
		}
	}

	/// <summary>
	/// Switches the screen stream to stream the screen of another virtual machine.
	/// </summary>
	/// <param name="vmDescriptor">A descriptor of the new virtual machine to stream the screen of. vmDescriptor != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: User has selected another tab, or has selected the VM screen page. The given virtual machine must not be the same as the current one.
	/// vmDescriptor != null <br/>
	/// Postcondition: On success, the stream is started and the returned exit code indicates success. <br/>
	/// On failure, the stream is not started and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> SwitchVirtualMachineAsync(SharedDefinitions.VmGeneralDescriptor vmDescriptor)
	{
		if (_vmDescriptor != null && _vmDescriptor.Id == vmDescriptor.Id)
		{
			return ExitCode.CallOnInvalidCondition;
		}

		_streamRunning = false;
		_vmDescriptor = vmDescriptor;

		return await StartStreamAsync();
	}

	/// <summary>
	/// Starts a screen stream of the current virtual machine.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine configured, (_vmDescriptor != null) and no stream is running. <br/>
	/// Postcondition: On success, a new stream of the virtual machines' screen is started, and the returned exit code indicates success. <br/>
	/// On failure, the stream is not started and the returned exit code indicates the error.
	/// </remarks>
	private async Task<ExitCode> StartStreamAsync()
	{
		if (_streamRunning)
		{
			return ExitCode.VmScreenStreamAlreadyRunning;
		}

		if (_vmDescriptor == null)
		{
			return ExitCode.CallOnInvalidCondition;
		}
		
		MessageResponseVmScreenStream? response = await ClientSvc.VirtualMachineStartScreenStreamAsync(_vmDescriptor!.Id);

		if (response == null)
		{
			_streamRunning = false;
			return ExitCode.MessageNotReceived;
		}
		
		if (response.Result == MessageResponseVmScreenStream.Status.Success || response.Result == MessageResponseVmScreenStream.Status.AlreadyStreaming)
		{
			_streamRunning = true;

			PixelFormat? pixelFormat = response.PixelFormat!.AsAvaloniaPixelFormat();
			if (pixelFormat == null)
			{
				return ExitCode.VmScreenStreamUnsupportedPixelFormat;
			}

			_pixelFormat = pixelFormat.Value;
			
			return ExitCode.Success;
		}

		return ExitCode.VmScreenStreamStartFailed;
	}

	/// <summary>
	/// Ends the current stream of the virtual machines' screen.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine configured (_vmDescriptor != null) and stream is running. <br/>
	/// Postcondition: On success, the stream is stopped and the returned exit code indicates success. <br/>
	/// If the server for some reason refuses to stop the stream, any incoming frames will be ignored. In this case the exit code will state VmScreenStreamStopFailed. <br/>
	/// On other failure, the stream is not stopped and the returned exit code will indicate the error.
	/// </remarks>
	private async Task<ExitCode> EndStreamAsync()
	{
		if (!_streamRunning)
		{
			return ExitCode.VmScreenStreamNotRunning;
		}

		if (_vmDescriptor == null)
		{
			return ExitCode.CallOnInvalidCondition;
		}
		
		/* TODO: Send an end screen stream request here */
		
		_streamRunning = false;

		return ExitCode.Success;
	}
}