using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using libvirt;
using Microsoft.Extensions.Logging;
using Server.Drives;
using Server.VirtualMachines;
using Shared;
using Shared.VirtualMachines;
using ILogger = Serilog.ILogger;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Server.Services;

public class VirtualMachineService
{
	private readonly ILogger _logger;
	private readonly DatabaseService _databaseService;
	private readonly UserService _userService;
	private readonly DriveService _driveService;
	
	/* By virtual machine ID's */
	private readonly ConcurrentDictionary<int, VirtualMachine> _aliveVirtualMachines;
	private readonly Connect _libvirtConnection;
	private int _vmUsedRamMiB = 0;

	public VirtualMachineService(ILogger logger, DatabaseService databaseService, UserService userService, DriveService driveService)
	{
		_logger = logger;
		_databaseService = databaseService;
		_userService = userService;
		_driveService = driveService;
		_aliveVirtualMachines = new ConcurrentDictionary<int, VirtualMachine>();
		_libvirtConnection = new Connect("qemu:///system");
	}

	/// <summary>
	/// Initializes the virtual machine service. Connects to libvirt.
	/// </summary>
	/// <remarks>
	/// Precondition: Service uninitialized. <br/>
	/// Postcondition: Service initialized, connected to libvirt.
	/// </remarks>
	public void Initialize()
	{
		if (!_libvirtConnection.IsOpen)
			_libvirtConnection.Open();
	}

	/// <summary>
	/// Closes the service. Attempts a graceful shutdown for all running virtual machines, after the timeout, they are destroyed.
	/// </summary>
	/// <remarks>
	/// Precondition: The service is initialized and running. Closing it is needed. <br/>
	/// Postcondition: Service uninitialized.
	/// </remarks>
	public async Task CloseAsync()
	{
		List<Task> vmsCloseTask = new List<Task>();
		foreach ((int _, VirtualMachine virtualMachine) in _aliveVirtualMachines)
		{
			vmsCloseTask.Add(virtualMachine.CloseAsync());
		}
		await Task.WhenAll(vmsCloseTask);
		
		_libvirtConnection.Close();
	}

	/// <summary>
	/// Get the state of a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine. id >= 1.</param>
	/// <returns>The state of the virtual machine, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID. id >= 1.<br/>
	/// Postcondition: On success, the state of the virtual machine is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<VmState> GetVmStateAsync(int id)
	{
		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			return virtualMachine.GetVmState();
		}
		
		return await _databaseService.GetVmStateAsync(id);
	}
	
	/// <summary>
	/// Creates a virtual machine in the database.
	/// </summary>
	/// <param name="userId">The ID of the owner user of the virtual machine. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <param name="operatingSystem">The operating system of the virtual machine.</param>
	/// <param name="cpuArchitecture">The CPU architecture (x86, x86-64, etc..) of the virtual machine.</param>
	/// <param name="ramSizeMiB">The amount of RAM storage for the virtual machine. Must be in valid range.
	/// ramSizeMiB > 0 &amp;&amp; ramSizeMiB &lt;= SharedDefinitions.VmRamSizeMbMax.</param>
	/// <param name="bootMode">The boot mode for the virtual machine. (UEFI or BIOS)</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID must exist,
	/// there should not be a virtual machine with the given name under this user. (name is unique).
	/// userId >= 1 &amp;&amp; name != null &amp;&amp; ramSizeMiB &lt;= SharedDefinitions.VmRamSizeMbMax. <br/>
	/// Postcondition: On success, a virtual machine with the given parameters is created. On failure, the returned exit code will indicate the error.
	/// </remarks>
	public async Task<ExitCode> CreateVirtualMachineAsync(int userId, string name,
		OperatingSystem operatingSystem, CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode)
	{
		return await _databaseService.CreateVmAsync(userId, name, operatingSystem, cpuArchitecture, ramSizeMiB, bootMode);
	}

	/// <summary>
	/// Checks if a virtual machine with the given name exists under a user with the given username.
	/// </summary>
	/// <param name="userId">The ID of the user to search for the VM under. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <returns>True if the virtual machine exists, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID must exist. userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: Returns true if the virtual machine exists, false if the virtual machine does not exist or on failure.
	/// </remarks>
	public async Task<bool> IsVmExistsAsync(int userId, string name)
	{
		return await _databaseService.IsVmExistsAsync(userId, name);
	}

	/// <summary>
	/// Power on (or wake up) a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to power on. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is powered on. <br/>
	/// On failure, the virtual machine is not powered on, and the retuned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> PowerOnVirtualMachineAsync(int id)
	{
		if (id < 1) return ExitCode.InvalidParameter;
		
		Task<VirtualMachineDescriptor?> vmDescriptor = _databaseService.GetVmDescriptorAsync(id);
		Task<DriveDescriptor[]?> vmDriveDescriptors = _databaseService.GetVmDriveDescriptorsAsync(id);
		await Task.WhenAll(vmDescriptor, vmDriveDescriptors);

		if (vmDescriptor.Result == null || vmDriveDescriptors.Result == null)
			return ExitCode.VmDoesntExist;
		
		if (vmDescriptor.Result.VmState == VmState.Running)
			return ExitCode.VmAlreadyRunning;

		if (!await AllocateVmRamAsync(vmDescriptor.Result.RamSizeMiB))
			return ExitCode.InsufficientMemory;
		
		VirtualMachine virtualMachine = new VirtualMachine(_logger.ForContext("Source", $"Virtual Machine {id}"), 
			_databaseService, _driveService, vmDescriptor.Result, vmDriveDescriptors.Result);

		bool addSuccess = false;
		try
		{
			addSuccess = _aliveVirtualMachines.TryAdd(id, virtualMachine);
		}
		catch (Exception)
		{
			_logger.Error($"Failed to add virtual machine {id} to _aliveVirtualMachines.");
		}

		if (!addSuccess)
		{
			FreeVmRam(vmDescriptor.Result.RamSizeMiB);
			return ExitCode.TooManyVmsRunning;	
		}
		
		ExitCode result = await virtualMachine.PowerOnAsync(_libvirtConnection);
		if(result != ExitCode.Success)
		{
			FreeVmRam(vmDescriptor.Result.RamSizeMiB);
			_aliveVirtualMachines.TryRemove(id, out _);
			return result;
		}

		virtualMachine.PoweredOff += OnVirtualMachinePoweredOff;
		virtualMachine.Crashed += OnVirtualMachineCrashed;

		await _userService.NotifyVirtualMachinePoweredOnAsync(id);
		
		return ExitCode.Success;
	}

	/// <summary>
	/// Powers off the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to power off. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists, and it is running. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is powered off and the returned exit code indicates success. <br/>
	/// On failure, the virtual machine remains in its previous state, and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> PowerOffVirtualMachineAsync(int id)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (!_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			return ExitCode.VmIsShutDown;
		}

		return await virtualMachine.PowerOffAsync();
	}

	/// <summary>
	/// Attempts to gracefully power off the given virtual machine.
	/// If the virtual machine ignores the shutdown signal, it is forced off. (destroyed)
	/// </summary>
	/// <param name="id">The ID of the virtual machine to power off. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists, and it is running. id >= 1. <br/>
	/// Postcondition: The virtual machine is shutdown.
	/// If the virtual machine responded to the shutdown signal, and did in fact shutdown, ExitCode.Success is returned.
	/// If the graceful shutdown has timed out, the virtual machine is forced shut down (destroyed) and ExitCode.VmShutdownTimeout is returned.
	/// </remarks>
	public async Task<ExitCode> PowerOffAndDestroyOnTimeoutAsync(int id)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (!_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			return ExitCode.VmIsShutDown;
		}

		return await virtualMachine.PowerOffAndDestroyOnTimeoutAsync();
	}

	/// <summary>
	/// Force shutdown the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to force off. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists, and it is powered on. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is forced off and is shutdown, and the returned exit code indicates success. <br/>
	/// On failure, the virtual machine is not forced off (remains in its previous state) and the returned exit code indicates the error.
	/// </remarks>
	public ExitCode ForceOffVirtualMachine(int id)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.Destroy();
			return ExitCode.Success;
		}
		
		return ExitCode.VmIsShutDown;
	}

	/// <summary>
	/// Subscribes the given handler to the event of receiving a new frame of the given virtual machine, compressed using the Brotli algorithm.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to subscribe to. id >= 1.</param>
	/// <param name="handler">The event handler to subscribe. handler != null</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID, and the virtual machine is alive. id >= 1 &amp;&amp; handler != null. <br/>
	/// Postcondition: On success, the given handler is registered and will receive new frames. <br/>
	/// On failure, the handler is not subscribed and the returned exit code will indicate the error.
	/// </remarks>
	public ExitCode SubscribeToVmNewBrotliFrameReceived(int id, EventHandler<VirtualMachineFrame> handler)
	{
		if (id < 1) 
			return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.BrotliFrameReceived += handler;
			return ExitCode.Success;
		}

		return ExitCode.VmIsShutDown;
	}
	
	/// <summary>
	/// Subscribes the given handler to the event of receiving a new frame of the given virtual machine, compressed using the GZip algorithm.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to subscribe to. id >= 1.</param>
	/// <param name="handler">The event handler to subscribe. handler != null</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID, and the virtual machine is alive. id >= 1 &amp;&amp; handler != null. <br/>
	/// Postcondition: On success, the given handler is registered and will receive new frames. <br/>
	/// On failure, the handler is not subscribed and the returned exit code will indicate the error.
	/// </remarks>
	public ExitCode SubscribeToVmNewGzipFrameReceived(int id, EventHandler<VirtualMachineFrame> handler)
	{
		if (id < 1) 
			return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.GzipFrameReceived += handler;
			return ExitCode.Success;
		}

		return ExitCode.VmIsShutDown;
	}

	/// <summary>
	/// Subscribes the given handler to the event of receiving a new audio packet of the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to subscribe to. id >= 1.</param>
	/// <param name="handler">The event handler to subscribe. handler != null</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID, and the virtual machine is alive. id >= 1 &amp;&amp; handler != null. <br/>
	/// Postcondition: On success, the given handler is registered and will receive new audio packets. <br/>
	/// On failure, the handler is not subscribed and the returned exit code will indicate the error.
	/// </remarks>
	public ExitCode SubscribeToVmAudioPacketReceived(int id, EventHandler<byte[]> handler)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.AudioPacketReceived += handler;
			return ExitCode.Success;
		}

		return ExitCode.VmIsShutDown;
	}

	/// <summary>
	/// Unsubscribe from the event of receiving a new frame from the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to unsubscribe from. id >= 1.</param>
	/// <param name="handler">The event handler that was subscribed. handler != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID, and it is not shutdown. id >= 1 &amp;&amp; handler != null <br/>
	/// Postcondition: On success, the handler is unsubscribed and will not receive new frame events. <br/>
	/// On failure, the event is not unsubscribed and the returned exit code will indicate the error.
	/// </remarks>
	public ExitCode UnsubscribeFromVmNewFrameReceived(int id, EventHandler<VirtualMachineFrame> handler)
	{
		if (id < 1) 
			return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.BrotliFrameReceived -= handler;
			virtualMachine.GzipFrameReceived -= handler;
			return ExitCode.Success;
		}
		
		return ExitCode.VmIsShutDown;
	}

	/// <summary>
	/// Unsubscribe from the event of receiving a new audio packet from the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to unsubscribe from. id >= 1.</param>
	/// <param name="handler">The event handler that was subscribed. handler != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID, and it is not shutdown. id >= 1 &amp;&amp; handler != null <br/>
	/// Postcondition: On success, the handler is unsubscribed and will not receive new audio packet events. <br/>
	/// On failure, the event is not unsubscribed and the returned exit code will indicate the error.
	/// </remarks>
	public ExitCode UnsubscribeFromVmAudioPacketReceived(int id, EventHandler<byte[]> handler)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.AudioPacketReceived -= handler;
			return ExitCode.Success;
		}
		
		return ExitCode.VmIsShutDown;
	}
	
	/// <summary>
	/// Enqueue a message to receive a fully updated frame.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to enqueue the message in. id >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists, and it is alive. id >= 1. <br/>
	/// Postcondition: On success, a message for getting a full frame is enqueued in the virtual machines' VNC message queue. <br/>
	/// On failure, the message is not queued and the returned exit code indicates the error.
	/// </remarks>
	public ExitCode EnqueueGetFullFrame(int id)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.EnqueueGetFullFrame();
			return ExitCode.Success;
		}
		
		return ExitCode.VmIsShutDown;
	}

	/// <summary>
	/// Enqueue a pointer movement message in the virtual machines' message queue.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to enqueue the message in. id >= 1.</param>
	/// <param name="position">The new pointer position. Must be in valid range. position != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The pointer has moved, the virtual machine needs to be notified about it. id >= 1 &amp;&amp; position != null. <br/>
	/// Postcondition: The pointer movement message in enqueued in the virtual machines' message queue.
	/// </remarks>
	public ExitCode EnqueuePointerMovement(int id, Point position)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.EnqueuePointerMovement(position);
			return ExitCode.Success;
		}
		
		return ExitCode.VmIsShutDown;
	}

	/// <summary>
	/// Enqueue a pointer button event in the virtual machines' message queue.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to enqueue the message id. id >= 1.</param>
	/// <param name="position">The current pointer position on the screen of the virtual machine. Must be in valid range. position != null.</param>
	/// <param name="pressedButtons">The currently pressed pointer buttons.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The state of one or more of the pointers buttons has changed, the virtual machine needs to be notified about it. <br/>
	/// position must be in valid range. id >= 1 &amp;&amp; position != null. <br/>
	/// Postcondition: The pointer button event is enqueued in the virtual machines' message queue.
	/// </remarks>
	public ExitCode EnqueuePointerButtonEvent(int id, Point position, int pressedButtons)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.EnqueuePointerButtonEvent(position, pressedButtons);
			return ExitCode.Success;
		}
		
		return ExitCode.VmIsShutDown;
	}

	/// <summary>
	/// Enqueue a keyboard key event in the virtual machines' message queue.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to enqueue the message id. id >= 1.</param>
	/// <param name="key">The key that the was pressed or released.</param>
	/// <param name="pressed">Whether the key was pressed or released. (true=pressed, false=released)</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The state of a keyboard key has changed. (was pressed or released) There is a running virtual machine with the given ID. id >= 1. <br/>
	/// Postcondition: The keyboard key event is enqueued in the virtual machines' message queue.
	/// </remarks>
	public ExitCode EnqueueKeyboardKeyEvent(int id, PhysicalKey key, bool pressed)
	{
		if (id < 1) return ExitCode.InvalidParameter;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			virtualMachine.EnqueueKeyboardKeyEvent(key, pressed);
			return ExitCode.Success;
		}
		
		return ExitCode.VmIsShutDown;
	}
	
	/// <summary>
	/// Get the pixel format used in a virtual machines' screen stream.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to get the screen stream pixel format of. id >= 1.</param>
	/// <returns>The used pixel format, or null on failure.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID, and it is not shut down. id >= 1. <br/>
	/// Postcondition: On success, the used pixel format is returned. On failure, null is returned.
	/// </remarks>
	public PixelFormat? GetScreenStreamPixelFormat(int id)
	{
		if (id < 1) return null;

		if (_aliveVirtualMachines.TryGetValue(id, out VirtualMachine? virtualMachine))
		{
			return virtualMachine.GetScreenStreamPixelFormat();
		}

		return null;
	}

	/// <summary>
	/// Handles the event that a virtual machine was powered off.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was powered off. id >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was powered off. id >= 1. <br/>
	/// Postcondition: The virtual machine is removed, relevant users are notified.
	/// </remarks>
	private void OnVirtualMachinePoweredOff(object? sender, int id)
	{
		if (!_aliveVirtualMachines.TryRemove(id, out VirtualMachine? virtualMachine)) return;
	
		_ = virtualMachine.CloseAsync();
		_ = _userService.NotifyVirtualMachinePoweredOffAsync(id);
		FreeVmRam(virtualMachine.RamSizeMiB);
	}

	/// <summary>
	/// Handles the event that a virtual machine has crashed.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that has crahsed. id >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine has crashed. id >= 1. <br/>
	/// Postcondition: The virtual machine is removed, relevant users are notified.
	/// </remarks>
	private void OnVirtualMachineCrashed(object? sender, int id)
	{
		if (!_aliveVirtualMachines.TryRemove(id, out VirtualMachine? virtualMachine)) return;
		
		_ = virtualMachine.CloseAsync();
		_ = _userService.NotifyVirtualMachineCrashedAsync(id);
		FreeVmRam(virtualMachine.RamSizeMiB);
	}

	/// <summary>
	/// Allocate RAM for a virtual machine.
	/// </summary>
	/// <param name="sizeMiB">The amount of RAM to allocate for a virtual machine, in MiB. sizeMiB >= 1.</param>
	/// <returns>True if allocated successfully, false otherwise.</returns>
	/// <remarks>
	/// Precondition: sizeMiB >= 1. <br/>
	/// Postcondition: On success, the given amount of memory is allocated and true is returned.
	/// On failure, no memory is allocated and false is returned.
	/// </remarks>
	private async Task<bool> AllocateVmRamAsync(int sizeMiB)
	{
		if (sizeMiB < 1) 
			return false;
		
		Dictionary<string, ulong> memInfo = new Dictionary<string, ulong>();
		await foreach (string line in File.ReadLinesAsync("/proc/meminfo").ConfigureAwait(false))
		{
			string[] keyValue = line.Split(':');
			if (keyValue.Length != 2)
				continue;
			
			int kbIndex = keyValue[1].IndexOf("kB", StringComparison.Ordinal);
			if (kbIndex < 0)
				continue;
			
			string key = keyValue[0].Trim();
			string valueStr = keyValue[1].Substring(0, kbIndex);
			if (!ulong.TryParse(valueStr, out ulong value))
				continue;

			memInfo.TryAdd(key, value);
		}

		if (!memInfo.TryGetValue("MemAvailable", out ulong memAvailableKb))
			return false;

		if (!memInfo.TryGetValue("SwapFree", out ulong swapFreeKb))
			return false;

		int availableMiB = (int)((memAvailableKb + swapFreeKb) / 1024);
		if (_vmUsedRamMiB + sizeMiB > availableMiB)
			return false;

		_vmUsedRamMiB += sizeMiB;
		_logger.Information($"Virtual machines use {_vmUsedRamMiB/1024f} GiB of RAM. {availableMiB/1024} GiB left.");
		return true;
	}

	/// <summary>
	/// Frees the given amount of RAM which was allocated using AllocateVmRamAsync().
	/// </summary>
	/// <param name="sizeMiB">The amount of RAM to free, in MiB. sizeMiB >= 1.</param>
	/// <remarks>
	/// Precondition: The given memory was allocated using AllocateVmRamAsync(). sizeMiB >= 1. <br/>
	/// Postcondition: The memory is freed. In the case that the given memory is greater than the currently
	/// allocated amount, the allocated amount is set to 0.
	/// </remarks>
	private void FreeVmRam(int sizeMiB) => _vmUsedRamMiB -= Math.Min(_vmUsedRamMiB, Math.Max(sizeMiB, 0));
}