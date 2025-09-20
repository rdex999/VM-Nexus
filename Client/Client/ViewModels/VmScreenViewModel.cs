using System.Threading.Tasks;
using Client.Services;
using Shared;
using Shared.Networking;
using PixelFormat = Avalonia.Platform.PixelFormat;

namespace Client.ViewModels;

public class VmScreenViewModel : ViewModelBase
{
	private bool _streamRunning = false;
	private SharedDefinitions.VmGeneralDescriptor? _vmDescriptor = null;
	private PixelFormat? _pixelFormat = null;
	
	public VmScreenViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.VmScreenFrameReceived += OnVmScreenFrameReceived;
	}

	private void OnVmScreenFrameReceived(object? sender, MessageInfoVmScreenFrame frame)
	{
		if (!_streamRunning || _vmDescriptor == null || frame.VmId != _vmDescriptor.Id || _pixelFormat == null)
		{
			return;
		}
	}

	public async Task<ExitCode> FocusAsync()
	{
		if (!_streamRunning && _vmDescriptor != null)
		{
			return await StartStreamAsync();
		}
		return ExitCode.Success;
	}
	
	public async Task UnfocusAsync()
	{
		if (_streamRunning)
		{
			await EndStreamAsync();
		}
	}

	public async Task<ExitCode> SwitchVirtualMachineAsync(SharedDefinitions.VmGeneralDescriptor vmDescriptor)
	{
		if (_vmDescriptor != null && _vmDescriptor.Id == vmDescriptor.Id)
		{
			return ExitCode.CallOnInvalidCondition;
		}
	
		bool wasStreamRunning = _streamRunning;
		bool isInitialDescSet = _vmDescriptor == null;

		Task<ExitCode>? endStreamTask = null;
		if (_streamRunning)
		{
			endStreamTask = EndStreamAsync();
		}
		_vmDescriptor = vmDescriptor;

		if (isInitialDescSet)
		{
			return await StartStreamAsync();
		}
		if (wasStreamRunning)
		{
			return (await Task.WhenAll(endStreamTask!, StartStreamAsync()))[1];		/* Return the result of StartStreamAsync */
		}

		return ExitCode.Success;
	}

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