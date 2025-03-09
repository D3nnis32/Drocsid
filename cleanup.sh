#!/bin/bash

# Stop and remove all API node containers
echo "Stopping and removing API node containers..."
docker ps -a | grep drocsid-api | awk '{print $1}' | xargs -r docker rm -f

# Stop and remove infrastructure containers (postgres and registry)
echo "Stopping infrastructure services..."
docker-compose down

# Optional: Remove API node volumes
if [ "$1" == "--volumes" ] || [ "$1" == "-v" ]; then
  echo "Removing API node volumes..."
  docker volume ls | grep api-node | awk '{print $2}' | xargs -r docker volume rm
  echo "Volumes removed."
fi

# Cleanup generated appsettings files
echo "Cleaning up generated appsettings files..."
rm -f node*-appsettings.json

echo "Cleanup complete!"