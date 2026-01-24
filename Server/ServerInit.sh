#!/bin/bash

# This script initializes the server for the first time - no need to run this on each server startup.
# Run the script when you are in the Server directory.
# Shell postgres login: sudo -u postgres psql postgres

mkdir -p "$HOME"/.VM-Nexus/DiskImages
mkdir -p "$HOME"/.VM-Nexus/OsDiskImages

if [ "$1" != "skip_db_setup" ]
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
  
  echo -e "Enter the PASSWORD for the postgres user when prompted"
  
  if ! sudo -u postgres createdb VM_Nexus_DB
  then
	echo -e "Creating database failed. Aborting."
	sudo systemctl stop postgresql
	exit 1
  fi

  sudo systemctl stop postgresql
fi

echo -e "Enter the server's IP:"
read IP

echo -e "Enter a password for the server.pfx key:"
read PASSWORD

mkdir -p Keys
echo -n "$PASSWORD" > Keys/server.pswd

echo -e "[req]" > Keys/san.cnf
echo -e "default_bits = 2048" >> Keys/san.cnf
echo -e "prompt = no" >> Keys/san.cnf
echo -e "distinguished_name = dn" >> Keys/san.cnf
echo -e "req_extensions = v3_req\n" >> Keys/san.cnf

echo -e "[dn]" >> Keys/san.cnf
echo -e "CN = $IP\n" >> Keys/san.cnf

echo -e "[v3_req]" >> Keys/san.cnf
echo -e "keyUsage = critical, digitalSignature, keyEncipherment" >> Keys/san.cnf
echo -e "extendedKeyUsage = serverAuth" >> Keys/san.cnf
echo -e "subjectAltName = @alt_names\n" >> Keys/san.cnf

echo -e "[alt_names]" >> Keys/san.cnf
echo -e "IP.1 = $IP" >> Keys/san.cnf

openssl genrsa -out Keys/server.key 2048
openssl genrsa -out Keys/ca.key 4096
openssl req -x509 -new -nodes -key Keys/ca.key -sha256 -days 3650 -subj "/CN=LocalDevCA" -out Keys/ca.crt
openssl req -new -key Keys/server.key -out Keys/server.csr -config Keys/san.cnf
openssl x509 -req -in Keys/server.csr -CA Keys/ca.crt -CAkey Keys/ca.key -CAcreateserial -out Keys/server.crt -days 825 -sha256 -extfile Keys/san.cnf -extensions v3_req
openssl pkcs12 -export -out Keys/server.pfx -inkey Keys/server.key -in Keys/server.crt -certfile Keys/ca.crt -passout pass:"$PASSWORD"

sudo rm -rf /etc/ssl/certs/server.crt
sudo rm -rf /etc/ssl/certs/ca.crt
sudo rm -rf /etc/ssl/private/server.key
sudo c_rehash

sudo cp Keys/server.crt /etc/ssl/certs
sudo cp Keys/ca.crt /etc/ssl/certs
sudo cp Keys/server.key /etc/ssl/private
sudo c_rehash

sudo systemctl stop nginx
sudo mkdir -p /etc/nginx/sites-enabled/
NGINX_CONFIG=$(cat <<EOF
map \$http_upgrade \$connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 443 ssl;
    server_name _;

    ssl_certificate /etc/ssl/certs/server.crt;
    ssl_certificate_key /etc/ssl/private/server.key;

    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers on;

    location / {
        proxy_pass http://$IP:5001/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection \$connection_upgrade;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_cache_bypass \$http_upgrade;
    }
}

server {
    listen 80;
    server_name _;
    return 301 https://\$host\$request_uri;
}
EOF
)
	
sudo tee /etc/nginx/sites-enabled/vmnexus.conf > /dev/null <<< "$NGINX_CONFIG"

sudo systemctl start nginx
echo -e "\nAdd: \"include sites-enabled/*.conf;\" in /etc/nginx/nginx.conf."
echo -e "Then start nginx if it was not started successfully here."
echo -e "For browsers, add Keys/ca.crt to authorities.\n"

sudo virsh net-define VmIsolatedNetwork.xml
sudo virsh net-autostart VM-Nexus-Isolated-Network
sudo virsh net-start VM-Nexus-Isolated-Network

exit