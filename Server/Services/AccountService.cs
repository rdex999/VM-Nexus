using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;
using Shared.VirtualMachines;

namespace Server.Services;

public class AccountService
{
	private readonly DatabaseService _databaseService;
	private readonly UserService _userService;
	private readonly VirtualMachineService _virtualMachineService;
	private readonly DriveService _driveService;

	public AccountService(DatabaseService databaseService, UserService userService,
		VirtualMachineService virtualMachineService, DriveService driveService)
	{
		_databaseService = databaseService;
		_userService = userService;
		_virtualMachineService = virtualMachineService;
		_driveService = driveService;
	}

	/// <summary>
	/// Deletes the users account. Stops all running VMs,
	/// deletes all drives, and transfers ownership of sub-users
	/// to the owner of the given user.
	/// </summary>
	/// <param name="userId">The ID of the user to delete. userId >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given username exists. userId >= 1. <br/>
	/// Postcondition: On success, the user is deleted. On failure,
	/// the user is not deleted and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> DeleteAccountAsync(int userId)
	{
		Task<VmGeneralDescriptor[]?> vmsTask = _databaseService.GetVmGeneralDescriptorsOfUserAsync(userId);
		Task<int[]?> driveIdsTask = _databaseService.GetDriveIdsOfUserAsync(userId);
		Task<int[]?> subUsersTask = _databaseService.GetSubUserIdsAsync(userId);
		Task<User?> userTask = _databaseService.GetUserAsync(userId);
		await Task.WhenAll(vmsTask, driveIdsTask, subUsersTask, userTask);

		VmGeneralDescriptor[]? vms = vmsTask.Result;
		int[]? driveIds = driveIdsTask.Result;
		int[]? subUsers = subUsersTask.Result;
		User? user = userTask.Result;
		int? newOwnerId = null;

		if (vms == null || driveIds == null || subUsers == null || user == null)
			return ExitCode.DataFetchFailed;
		
		if (user is SubUser sUser)
			newOwnerId = sUser.OwnerId;

		List<Task<ExitCode>> shutdownTasks = new List<Task<ExitCode>>();
		foreach (VmGeneralDescriptor vm in vms)
		{
			if (vm.State == VmState.Running)
				shutdownTasks.Add(_virtualMachineService.PowerOffAndDestroyOnTimeoutAsync(vm.Id));
		}

		await Task.WhenAll(shutdownTasks);

		List<Task<ExitCode>> vmDeleteTasks = new List<Task<ExitCode>>();
		foreach (VmGeneralDescriptor vm in vms)
			vmDeleteTasks.Add(_databaseService.DeleteVmAsync(vm.Id));

		List<Task<ExitCode>> driveDeleteTasks = new List<Task<ExitCode>>();
		foreach (int driveId in driveIds)
			driveDeleteTasks.Add(_driveService.DeleteDriveAsync(driveId));

		List<Task<ExitCode>> subUsersUpdateTasks = new List<Task<ExitCode>>();
		foreach (int subUserId in subUsers)
			subUsersUpdateTasks.Add(_databaseService.UpdateUserOwnerAsync(subUserId, newOwnerId));

		await Task.WhenAll(vmDeleteTasks.Concat(driveDeleteTasks).Concat(subUsersUpdateTasks));

		await _userService.NotifyUserDeletedAsync(userId);
		ExitCode result = await _databaseService.DeleteUserAsync(userId);
		if (result != ExitCode.Success)
			return ExitCode.DatabaseOperationFailed;

		if (newOwnerId != null)
		{
			/* Get the sub-users of the new owner after transferring him more sub-users. */
			SubUser[]? updatedNewOwnersSubUsers = await _databaseService.GetSubUsersAsync(newOwnerId.Value);
			if (updatedNewOwnersSubUsers == null)
				return ExitCode.DatabaseOperationFailed;

			/* The new owner of the sub-users of the deleted user, should be notified that he has "new" sub-users. */
			foreach (SubUser subUser in updatedNewOwnersSubUsers)
			{
				/* Notify only if the sub user is "new" for the owner (If the subUser is one of those that their owner was just updated) */
				if (subUsers.Contains(subUser.Id))
					_userService.NotifySubUserCreated(subUser);
			}
		}
		
		return ExitCode.Success;
	}
}