@echo off
setlocal enabledelayedexpansion

echo ===== Drocsid Deployment Script for Windows =====
echo.
echo Current directory: %cd%

:: Default number of nodes
set NUM_NODES=3
if not "%1"=="" set NUM_NODES=%1

:: Verify required files exist
echo Checking required files...
if not exist "Dockerfile.api" (
    echo ERROR: Dockerfile.api not found in current directory.
    exit /b 1
)

if not exist "Dockerfile.registry" (
    echo ERROR: Dockerfile.registry not found in current directory.
    exit /b 1
)

if not exist "node-template.json" (
    echo ERROR: node-template.json not found in current directory.
    exit /b 1
)

if not exist "docker-compose.yml" (
    echo ERROR: docker-compose.yml not found in current directory.
    exit /b 1
)

if not exist "registry-appsettings.json" (
    echo ERROR: registry-appsettings.json not found in current directory.
    exit /b 1
)

:: Build the plugins first
echo Building plugins...
if exist "VoiceChatPlugin" (
    echo Building VoiceChatPlugin...
    dotnet build VoiceChatPlugin/VoiceChatPlugin.csproj -c Release
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to build VoiceChatPlugin.
        exit /b 1
    )
)

if exist "WhiteboardPlugin" (
    echo Building WhiteboardPlugin...
    dotnet build WhiteboardPlugin/WhiteboardPlugin.csproj -c Release
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to build WhiteboardPlugin.
        exit /b 1
    )
)

:: Build UI if needed
if exist "UI" (
    echo Building UI application...
    dotnet publish -c Release -o ./build/UI UI/UI.csproj
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to build UI application.
        exit /b 1
    )
    echo UI built successfully.
    echo.
)

:: Build Docker images
echo Building Docker images...

:: Build the registry service image
echo Building registry service image...
docker build -t drocsid-registry -f Dockerfile.registry .
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build drocsid-registry Docker image.
    exit /b 1
)

:: Build the API image
echo Building API service image...
docker build -t drocsid-api -f Dockerfile.api .
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build drocsid-api Docker image.
    exit /b 1
)

echo Verifying images were built...
docker images | findstr drocsid
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Docker images not found after build.
    exit /b 1
)

echo Docker images built successfully!
echo.

:: Start infrastructure with docker-compose
echo Starting infrastructure (PostgreSQL and Registry Service)...
docker-compose up -d
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to start infrastructure with docker-compose.
    exit /b 1
)

echo Waiting for registry service to be ready...
timeout /t 10 /nobreak > nul

echo.
echo Starting %NUM_NODES% API nodes...
echo.

:: Calculate the last port
set /a LAST_PORT=5185+%NUM_NODES%

:: Create appsettings files and start containers for each node
for /l %%i in (1,1,%NUM_NODES%) do (
    set NODE_ID=node%%i
    set NODE_REGION=region%%i
    set NODE_TAG=storage-%%i
    set /a NODE_PORT=5185+%%i
    set NODE_HOSTNAME=drocsid-storage-%%i
    set NODE_ENDPOINT=http://localhost:!NODE_PORT!
    
    echo Creating settings for node%%i with port !NODE_PORT!...
    
    :: Create a copy of the template - using copy instead of type
    copy /Y "node-template.json" "node%%i-appsettings.json"
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to copy template file for node%%i.
        exit /b 1
    )
    
    :: Replace placeholders in the config file using a simpler method
    powershell -Command "(Get-Content 'node%%i-appsettings.json') | ForEach-Object { $_ -replace 'NODE_ID_PLACEHOLDER', 'node%%i' -replace 'NODE_REGION_PLACEHOLDER', 'region%%i' -replace 'NODE_TAG_PLACEHOLDER', 'storage-%%i' -replace 'NODE_ENDPOINT_PLACEHOLDER', 'http://localhost:!NODE_PORT!' -replace 'NODE_HOSTNAME_PLACEHOLDER', 'drocsid-storage-%%i' } | Set-Content 'node%%i-appsettings.json'"
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to update configuration for node%%i.
        exit /b 1
    )

    :: Verify the file was created
    if not exist "node%%i-appsettings.json" (
        echo ERROR: Failed to create node%%i-appsettings.json
        exit /b 1
    )

    :: Get the drocsid network name
    for /f %%n in ('docker network ls --filter "name=drocsid" --format "{{.Name}}" ^| findstr drocsid') do set NETWORK_NAME=%%n
    
    echo Starting container for node%%i on port !NODE_PORT!...
    
    :: Stop and remove existing container if it exists
    docker rm -f "drocsid-api-%%i" >nul 2>&1
    
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
    
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to start container for node%%i.
        echo Verify that the drocsid-api image exists: 
        docker images | findstr drocsid-api
        exit /b 1
    )
        
    echo Successfully started node%%i on port !NODE_PORT!
    echo.
)

:: Create a UI launcher if we built the UI
if exist "build\UI\UI.exe" (
    echo Creating UI launcher...
    echo @echo off > build\run-ui.bat
    echo start "" "UI\UI.exe" >> build\run-ui.bat
    echo UI launcher created at: %cd%\build\run-ui.bat
    echo.
)

echo ===== Deployment complete! =====
echo.
if exist "build\UI\UI.exe" (
    echo UI application: %cd%\build\UI\UI.exe
    echo Run 'build\run-ui.bat' to start the UI
)
echo Registry service: http://localhost:5261
echo API nodes: %NUM_NODES% nodes running on ports 5186-!LAST_PORT!
echo.

echo Would you like to start the UI now? (Y/N)
set /p START_UI=
if /i "%START_UI%"=="Y" (
    if exist "build\UI\UI.exe" (
        echo Starting UI application...
        start "" "build\UI\UI.exe"
    ) else (
        echo UI application not found.
    )
)

echo.
echo For debugging, here are the running containers:
docker ps | findstr drocsid