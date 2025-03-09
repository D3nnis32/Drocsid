#!/bin/bash

# Build the registry service and API images
echo "Building registry service image..."
docker build -t drocsid-registry -f Dockerfile.registry .

echo "Building API node image..."
docker build -t drocsid-api -f Dockerfile.api .

echo "Images built successfully!"