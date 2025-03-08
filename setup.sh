# Start the services
docker-compose up -d

echo "Waiting for services to start..."
sleep 10

# Check if services are running
echo "Checking if services are running..."
docker-compose ps

echo "Setup complete. Your Drocsid distributed file storage system is now running!"
echo "Registry service: http://localhost:5261"
echo "API Node 1: http://localhost:5186"
echo "API Node 2: http://localhost:5187"
echo "API Node 3: http://localhost:5188"
echo "Access Swagger UI at http://localhost:5261/swagger or http://localhost:5186/swagger"