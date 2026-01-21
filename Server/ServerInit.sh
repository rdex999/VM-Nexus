#!/bin/bash

# This script initializes the server for the first time - no need to run this on each server startup.
# Run the script when you are in the Server directory.
# Shell postgres login: sudo -u postgres psql postgres

mkdir -p "$HOME"/.VM-Nexus/DiskImages
mkdir -p "$HOME"/.VM-Nexus/OsDiskImages

if [ "$1" != "key_gen_only" ]
then
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
fi

mkdir -p Keys
echo -e "Enter a password for the server.pfx key:"
read password

echo -e "Enter the server's IP:"
read ip

echo -n "$password" > Keys/server.pswd

echo -e "[req]" > Keys/san.cnf
echo -e "default_bits = 2048" >> Keys/san.cnf
echo -e "prompt = no" >> Keys/san.cnf
echo -e "distinguished_name = dn" >> Keys/san.cnf
echo -e "req_extensions = v3_req\n" >> Keys/san.cnf

echo -e "[dn]" >> Keys/san.cnf
echo -e "CN = $ip\n" >> Keys/san.cnf

echo -e "[v3_req]" >> Keys/san.cnf
echo -e "keyUsage = critical, digitalSignature, keyEncipherment" >> Keys/san.cnf
echo -e "extendedKeyUsage = serverAuth" >> Keys/san.cnf
echo -e "subjectAltName = @alt_names\n" >> Keys/san.cnf

echo -e "[alt_names]" >> Keys/san.cnf
echo -e "IP.1 = $ip" >> Keys/san.cnf

openssl genrsa -out Keys/server.key 2048
openssl req -new -key Keys/server.key -out Keys/server.csr -config Keys/san.cnf
openssl x509 -req -in Keys/server.csr -signkey Keys/server.key -out Keys/server.crt -days 365 -extfile Keys/san.cnf
openssl pkcs12 -export -out Keys/server.pfx -inkey Keys/server.key -in Keys/server.crt -passout "pass:$password"

sudo cp Keys/server.crt /etc/ssl/certs
sudo c_rehash

exit