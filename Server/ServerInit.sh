#!/bin/bash

# This script initializes the server for the first time - no need to run this on each server startup.
# Run the script when you are in the Server directory.
# Shell postgres login: sudo -u postgres psql postgres

mkdir DiskImages

if ! sudo -u postgres initdb --locale en_US.UTF-8 -D /var/lib/postgres/data
then
  echo -e "initdb failed. Aborting."
  exit 1
fi

if ! sudo systemctl start postgresql
then
  echo -e "postgresql.service start failed. Aborting."
  exit 1
fi 

echo -e "Enter the password for the postgres user when prompted"

if ! sudo -u postgres createdb VM_Nexus_DB
then
  echo -e "Creating database failed. Aborting."
  sudo systemctl stop postgresql
  exit 1
fi

sudo systemctl stop postgresql

exit