@echo off
setlocal enabledelayedexpansion

echo ===== Add Drocsid API Nodes =====

:: Check if the required number of nodes is provided
if "%1"=="" (
    echo Usage: %0 ^<number_of_nodes_to_add^>
    echo Example: %0 2   # Adds 2 more nodes
    exit /b 1
)

set NUM_TO_ADD=%1

:: Get highest existing node ID
set HIGHEST_ID=0
for /f "tokens=*" %%i in ('docker ps -a ^| findstr drocsid-api ^| findstr /R "drocsid-api-[0-9]*" ^| sort') do (
    for /f "tokens=3 delims=-" %%j in ("%%i") do (
        if %%j GTR !HIGHEST_ID! set HIGHEST_ID=%%j
    )
)

if %HIGHEST_ID%==0 (
    :: No existing nodes, start from 1
    set START_ID=1
) else (
    :: Start from the next ID after the highest existing one
    set /a START_ID=HIGHEST_ID+1
)

set /a END_ID=START_ID+NUM_TO_ADD-1

echo Adding %NUM_TO_ADD% new nodes starting from ID %START_ID%...

:: Create appsettings files and start containers for each new node
for /l %%i in (%START_ID%,1,%END_ID%) do (
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

echo Successfully added %NUM_TO_ADD% new nodes (from node%START_ID% to node%END_ID%)

:: Count total running nodes
for /f %%i in ('docker ps ^| findstr drocsid-api ^| find /c /v ""') do set TOTAL_NODES=%%i
echo Now running %TOTAL_NODES% API nodes in total.