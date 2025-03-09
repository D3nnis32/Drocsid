#!/bin/bash

# Make sure the scripts are executable
chmod +x build-images.sh
chmod +x start-node.sh

# Step 1: Build Docker images
./build-images.sh

# Step 2: Start infrastructure (PostgreSQL and Registry Service)
echo "Starting infrastructure services..."
docker-compose up -d --build

# Wait for registry service to be ready
echo "Waiting for registry service to be ready..."
sleep 10

# Step 3: Start API nodes
if [ $# -lt 1 ]; then
  # Default: Start 3 nodes
  echo "Starting 3 API nodes (default)..."
  ./start-node.sh 3
else
  # Start the specified number of nodes
  echo "Starting $1 API nodes..."
  ./start-node.sh $1
fi

echo "Deployment complete!"
echo "You can access the registry service at http://localhost:5261"
echo "API nodes are accessible at ports starting from 5186"