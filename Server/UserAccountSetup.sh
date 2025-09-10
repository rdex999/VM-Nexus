#!/bin/bash

# Run this script in new virtual machines to setup a new user and update the root user password.

if [[ $EUID -ne 0 ]]; then
    echo -e "This script must be run as root." >&2
    echo -e "\$ sudo $0"
    exit 1
fi

echo -e "\nWelcome to the user account setup script."
echo -e "Here we will create a new user for you, set its password and change the root user password.\n"

read -p "Enter a username for the new user: " new_username
if [ -z "$new_username" ]
then
	echo -e "The username must not be empty." >&2
	exit 1
fi

echo -e "\nEnter the password for the new user when prompted.\n"
if ! adduser "$new_username"
then
	echo -e "Failure - Quitting." >&2
	exit 1
fi

if ! usermod -aG sudo "$new_username"
then
	echo -e "Adding the user to the sudo group has failed. Quitting."
	userdel -r "$new_username"
	exit 1
fi


echo -e "\nNow we will update the root user password."
echo -e "Enter a new password when prompted.\n"
if ! passwd
then
	echo -e "Updating the root user password has failed. Quitting."
	userdel -r "$new_username"
	exit 1
fi

echo -e "\nThe new user was created successfully!"
echo -e "You may now log in as the new user."
echo -e "Note: you might want to delete the setup_user user."
echo -e "You can do so by executing: sudo userdel -r setup_user\n"