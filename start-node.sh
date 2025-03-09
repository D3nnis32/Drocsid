#!/bin/bash

# Check if the required number of nodes is provided
if [ $# -lt 1 ]; then
  echo "Usage: $0 <number_of_nodes> [starting_node_id_number]"
  echo "Example: $0 3     # Creates node1, node2, node3"
  echo "Example: $0 2 4   # Creates node4, node5"
  exit 1
fi

NUM_NODES=$1
START_ID=${2:-1}  # Default to 1 if not provided

# Calculate the last node ID
END_ID=$((START_ID + NUM_NODES - 1))

# Create appsettings files and start containers for each node
for (( i=START_ID; i<=END_ID; i++ ))
do
  NODE_ID="node$i"
  NODE_REGION="region$i"
  NODE_TAG="storage-$i"
  NODE_PORT=$((5185 + i))
  NODE_HOSTNAME="drocsid-storage-$i"
  NODE_ENDPOINT="http://localhost:$NODE_PORT"
  
  echo "Creating settings for $NODE_ID with port $NODE_PORT..."
  
  # Create appsettings file from template
  sed \
    -e "s/NODE_ID_PLACEHOLDER/$NODE_ID/g" \
    -e "s/NODE_REGION_PLACEHOLDER/$NODE_REGION/g" \
    -e "s/NODE_TAG_PLACEHOLDER/$NODE_TAG/g" \
    -e "s#NODE_ENDPOINT_PLACEHOLDER#$NODE_ENDPOINT#g" \
    -e "s/NODE_HOSTNAME_PLACEHOLDER/$NODE_HOSTNAME/g" \
    node-template.json > "$NODE_ID-appsettings.json"

  # Create and start the container
  docker run -d \
    --name "drocsid-api-$i" \
    --network drocsid-network \
    -e ASPNETCORE_ENVIRONMENT=Production \
    -e ASPNETCORE_URLS=http://+:5186 \
    -p "$NODE_PORT:5186" \
    -v "$(pwd)/$NODE_ID-appsettings.json:/app/appsettings.json" \
    -v "api-node$i-storage:/app/FileStorage" \
    --restart on-failure \
    drocsid-api

  echo "Started node $NODE_ID on port $NODE_PORT"
done

echo "Successfully started $NUM_NODES nodes (from $START_ID to $END_ID)"