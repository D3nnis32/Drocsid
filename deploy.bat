@echo off
setlocal enabledelayedexpansion

echo ===== Drocsid Deployment Script for Windows =====

:: Default number of nodes
set NUM_NODES=3
if not "%1"=="" set NUM_NODES=%1

echo Building Docker images...
:: Build the registry service image
docker build -t drocsid-registry -f Dockerfile.registry .
:: Build the API image (now includes plugin building)
docker build -t drocsid-api -f Dockerfile.api .
echo Docker images built successfully!

echo Starting infrastructure (PostgreSQL and Registry Service)...
docker-compose up -d
echo Waiting for registry service to be ready...
timeout /t 10 /nobreak > nul

echo Starting %NUM_NODES% API nodes...

:: Create appsettings files and start containers for each node
for /l %%i in (1,1,%NUM_NODES%) do (
    set NODE_ID=node%%i
    set NODE_REGION=region%%i
    set NODE_TAG=storage-%%i
    set /a NODE_PORT=5185+%%i
    set NODE_HOSTNAME=drocsid-storage-%%i
    set NODE_ENDPOINT=http://localhost:!NODE_PORT!
    
    echo Creating settings for node%%i with port !NODE_PORT!...
    
    :: Create a copy of the template
    type node-template.json > node%%i-appsettings.json
    
    :: Replace placeholders in the config file
    powershell -Command "(Get-Content node%%i-appsettings.json) -replace 'NODE_ID_PLACEHOLDER', 'node%%i' | Set-Content node%%i-appsettings.json"
    powershell -Command "(Get-Content node%%i-appsettings.json) -replace 'NODE_REGION_PLACEHOLDER', 'region%%i' | Set-Content node%%i-appsettings.json"
    powershell -Command "(Get-Content node%%i-appsettings.json) -replace 'NODE_TAG_PLACEHOLDER', 'storage-%%i' | Set-Content node%%i-appsettings.json"
    powershell -Command "(Get-Content node%%i-appsettings.json) -replace 'NODE_ENDPOINT_PLACEHOLDER', 'http://localhost:!NODE_PORT!' | Set-Content node%%i-appsettings.json"
    powershell -Command "(Get-Content node%%i-appsettings.json) -replace 'NODE_HOSTNAME_PLACEHOLDER', 'drocsid-storage-%%i' | Set-Content node%%i-appsettings.json"

    :: Start the container
    :: Get the actual network name
    for /f %%n in ('docker network ls --filter "name=drocsid" --format "{{.Name}}" ^| findstr drocsid') do set NETWORK_NAME=%%n
    
    docker run -d ^
        --name "drocsid-api-%%i" ^
        --network !NETWORK_NAME! ^
        -e ASPNETCORE_ENVIRONMENT=Production ^
        -e ASPNETCORE_URLS=http://+:5186 ^
        -p "!NODE_PORT!:5186" ^
        -v "%cd%\node%%i-appsettings.json:/app/appsettings.json" ^
        -v "api-node%%i-storage:/app/FileStorage" ^
        --restart on-failure ^
        drocsid-api
        
    echo Started node%%i on port !NODE_PORT!
)

echo Deployment complete!
echo You can access the registry service at http://localhost:5261
echo API nodes are accessible at ports starting from 5186
echo Total nodes deployed: %NUM_NODES%

echo.
echo To test if plugins are properly loaded, try these API endpoints:
echo - http://localhost:5186/api/plugins (for a list of all available plugins)
echo - http://localhost:5186/api/plugins/channel/{channelId}/communication (for voice chat plugin)
echo - http://localhost:5186/api/plugins/channel/{channelId}/collaboration (for whiteboard plugin)