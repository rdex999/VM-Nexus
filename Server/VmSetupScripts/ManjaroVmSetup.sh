#!/bin/bash

# Run this script in new virtual machines to setup a new user and update the root user password.

setup_user_username="setup-user"

if [[ $EUID -ne 0 ]]; then
    echo -e "\nThis script must be run as root." >&2
    echo -e "\$ sudo $0"
    exit 1
fi

echo -e "\nWelcome to the user account setup script."
echo -e "Here we will create a new user for you, change its and the root user password and resize the root filesystem (expand it).\n"

read -p "Enter a username for the new user: " new_username
if [ -z "$new_username" ]
then
	echo -e "\nThe username must not be empty." >&2
	exit 2
fi

echo -e "\nEnter the password for the new user when prompted.\n"
if ! useradd -m "$new_username"
then
	echo -e "\nFailure - Quitting." >&2
	exit 3
fi

if ! passwd "$new_username"
then
  echo -e "\nFailure - Quitting." >&2
  exit 4
fi

if ! usermod -aG wheel "$new_username"
then
	echo -e "\nAdding the user to the sudo group has failed. Quitting."
	userdel -r "$new_username"
	exit 5
fi

echo -e "\nNow we will update the root user password."
echo -e "Enter a new password when prompted.\n"
if ! passwd
then
	echo -e "\nUpdating the root user password has failed. Quitting."
	userdel -r "$new_username"
	exit 6
fi


root_partition="/dev/vda3"

if ! resize2fs $root_partition
then
	echo -e "\nFilesystem resizing has failed. Quitting."
	userdel -r "$new_username"
	exit 7
fi

if ! userdel --remove --force $setup_user_username
then
	echo -e "\nDeleting $setup_user_username has failed. Quiting."
	userdel -r "$new_username"
	exit 8
fi

echo -e "\nThe new user was created successfully!"
echo -e "The system will now reboot, after that tou may now log in as the new user."
sleep 3
reboot