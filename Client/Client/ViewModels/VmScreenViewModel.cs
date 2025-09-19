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
	
	public void Unfocus()
	{
		if (_streamRunning)
		{
			EndStream();
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
		
		if (_streamRunning)
		{
			EndStream();
		}
		_vmDescriptor = vmDescriptor;

		if (wasStreamRunning || isInitialDescSet)
		{
			return await StartStreamAsync();
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
	
	private void EndStream()
	{
		_streamRunning = false;
	}
}