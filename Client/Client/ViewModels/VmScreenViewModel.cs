using System.Threading.Tasks;
using Client.Services;
using Shared;
using Shared.Networking;

namespace Client.ViewModels;

public class VmScreenViewModel : ViewModelBase
{
	private bool _streamRunning = false;
	private SharedDefinitions.VmGeneralDescriptor? _vmDescriptor = null;
	
	public VmScreenViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
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
		
		MessageResponseVmScreenStream.Status result = await ClientSvc.VirtualMachineStartScreenStreamAsync(_vmDescriptor!.Id);
		
		_streamRunning = result == MessageResponseVmScreenStream.Status.Success || result == MessageResponseVmScreenStream.Status.AlreadyStreaming;

		if (result == MessageResponseVmScreenStream.Status.Success)
		{
			return ExitCode.Success;
		}

		if (result == MessageResponseVmScreenStream.Status.AlreadyStreaming)
		{
			return ExitCode.VmScreenStreamAlreadyRunning;
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