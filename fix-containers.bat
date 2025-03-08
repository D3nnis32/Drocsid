@echo off
echo Fixing Docker setup issues...

echo 1. Stopping and removing existing containers...
docker-compose down

echo 2. Creating databases manually...
docker-compose up -d postgres
timeout /t 10 /nobreak
echo Creating drocsid_registry database...
docker exec drocsid-postgres psql -U postgres -c "CREATE DATABASE drocsid_registry;"
docker exec drocsid-postgres psql -U postgres -c "CREATE DATABASE drocsid;"

echo 3. Updating configuration files...
echo Updating registry-appsettings.json to remove HTTPS URL...
powershell -Command "(Get-Content registry-appsettings.json) -replace 'http://localhost:5000;https://localhost:5001', 'http://*:5261' | Set-Content registry-appsettings.json"

echo 4. Fixing program.cs in registry service...
powershell -Command "(Get-Content RegistryService/Program.cs) -replace 'builder.WebHost.UseUrls\(\"http://localhost:5000;https://localhost:5001\"\);', 'builder.WebHost.UseUrls(\"http://*:5261\");' | Set-Content RegistryService/Program.cs"

echo 5. Removing existing images...
docker-compose down
docker rmi drocsidhenrikdennis2025-api-node-1 drocsidhenrikdennis2025-api-node-2 drocsidhenrikdennis2025-api-node-3 drocsidhenrikdennis2025-registry-service

echo 6. Building and starting services...
docker-compose build --no-cache
docker-compose up -d

echo 7. Waiting for services to start...
timeout /t 15 /nobreak

echo 8. Checking service status...
docker-compose ps

echo Done! Services should now be running without errors.
echo.
echo If you still encounter issues, check the logs with:
echo docker-compose logs