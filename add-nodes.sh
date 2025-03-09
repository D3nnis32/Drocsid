#!/bin/bash

# Check if the required number of nodes is provided
if [ $# -lt 1 ]; then
  echo "Usage: $0 <number_of_nodes_to_add>"
  echo "Example: $0 2   # Adds 2 more nodes"
  exit 1
fi

# Get the highest existing node ID
HIGHEST_ID=$(docker ps -a | grep drocsid-api | awk '{print $NF}' | sed 's/drocsid-api-//' | sort -n | tail -1)

if [ -z "$HIGHEST_ID" ]; then
  # No existing nodes, start from 1
  START_ID=1
else
  # Start from the next ID after the highest existing one
  START_ID=$((HIGHEST_ID + 1))
fi

NUM_NODES=$1

echo "Adding $NUM_NODES new nodes starting from ID $START_ID..."

# Use the start-node.sh script to add the new nodes
./start-node.sh $NUM_NODES $START_ID

echo "Added $NUM_NODES new nodes. Now running $(docker ps | grep drocsid-api | wc -l) API nodes in total."