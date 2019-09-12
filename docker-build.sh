docker --version || wget -qO- https://get.docker.com/ | sh
sleep 1

docker build -t fssql -t .