@echo off
setlocal enabledelayedexpansion

echo ===== Drocsid Build and Run Script =====
echo.

:: Check for command line arguments
set BUILD_BACKEND=true
set BUILD_UI=true
set RUN_UI=true
set NODE_COUNT=3

:parse_args
if "%1"=="" goto end_parse_args
if /i "%1"=="--no-backend" set BUILD_BACKEND=false
if /i "%1"=="--no-ui" (
    set BUILD_UI=false
    set RUN_UI=false
)
if /i "%1"=="--no-run-ui" set RUN_UI=false
if /i "%1"=="--nodes" (
    set NODE_COUNT=%2
    shift
)
shift
goto parse_args
:end_parse_args

echo Configuration:
echo - Build backend: %BUILD_BACKEND%
echo - Build UI: %BUILD_UI%
echo - Run UI: %RUN_UI%
echo - Number of nodes: %NODE_COUNT%
echo.

:: Create build directory if it doesn't exist
if not exist "build" mkdir build

:: Build plugins first
if "%BUILD_BACKEND%"=="true" (
    echo Step 1: Building plugins...
    if exist "VoiceChatPlugin" (
        echo   - Building VoiceChatPlugin...
        dotnet build VoiceChatPlugin/VoiceChatPlugin.csproj -c Release
        if !ERRORLEVEL! NEQ 0 (
            echo ERROR: Failed to build VoiceChatPlugin.
            exit /b 1
        )
    ) else (
        echo   - VoiceChatPlugin directory not found.
    )

    if exist "WhiteboardPlugin" (
        echo   - Building WhiteboardPlugin...
        dotnet build WhiteboardPlugin/WhiteboardPlugin.csproj -c Release
        if !ERRORLEVEL! NEQ 0 (
            echo ERROR: Failed to build WhiteboardPlugin.
            exit /b 1
        )
    ) else (
        echo   - WhiteboardPlugin directory not found.
    )
)

:: Build UI if needed
if "%BUILD_UI%"=="true" (
    echo Step 2: Building UI application...
    if exist "UI" (
        dotnet publish -c Release -o ./build/UI UI/UI.csproj
        if !ERRORLEVEL! NEQ 0 (
            echo ERROR: Failed to build UI application.
            exit /b 1
        )
        
        :: Create UI launcher
        echo @echo off > build\run-ui.bat
        echo start "" "%cd%\build\UI\UI.exe" >> build\run-ui.bat
        echo   - UI launcher created at: %cd%\build\run-ui.bat
        
        :: Create desktop shortcut
        echo   - Creating desktop shortcut...
        powershell -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut([System.Environment]::GetFolderPath('Desktop') + '\Drocsid.lnk'); $Shortcut.TargetPath = '%cd%\build\UI\UI.exe'; $Shortcut.Save()"
    ) else (
        echo   - UI directory not found. Skipping UI build.
        set BUILD_UI=false
        set RUN_UI=false
    )
)

:: Build and deploy backend
if "%BUILD_BACKEND%"=="true" (
    echo Step 3: Building and deploying backend services...
    
    :: Check if Docker is running
    docker ps >nul 2>&1
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Docker is not running. Please start Docker Desktop.
        exit /b 1
    )
    
    :: Check for required files
    echo   - Checking required files...
    set MISSING_FILES=false
    
    if not exist "Dockerfile.api" (
        echo     ERROR: Dockerfile.api not found.
        set MISSING_FILES=true
    )
    
    if not exist "Dockerfile.registry" (
        echo     ERROR: Dockerfile.registry not found.
        set MISSING_FILES=true
    )
    
    if not exist "node-template.json" (
        echo     ERROR: node-template.json not found.
        set MISSING_FILES=true
    )
    
    if not exist "docker-compose.yml" (
        echo     ERROR: docker-compose.yml not found.
        set MISSING_FILES=true
    )
    
    if not exist "registry-appsettings.json" (
        echo     ERROR: registry-appsettings.json not found.
        set MISSING_FILES=true
    )
    
    if "%MISSING_FILES%"=="true" (
        echo ERROR: Missing required files for backend deployment.
        exit /b 1
    )
    
    :: Build Docker images
    echo   - Building Docker images...
    
    echo     Building registry service image...
    docker build -t drocsid-registry -f Dockerfile.registry .
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to build drocsid-registry Docker image.
        exit /b 1
    )
    
    echo     Building API service image...
    docker build -t drocsid-api -f Dockerfile.api .
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to build drocsid-api Docker image.
        exit /b 1
    )
    
    echo     Docker images built successfully!
    
    :: Start infrastructure
    echo   - Starting infrastructure (PostgreSQL and Registry Service)...
    docker-compose up -d
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Failed to start infrastructure with docker-compose.
        exit /b 1
    )
    
    echo   - Waiting for registry service to be ready...
    timeout /t 10 /nobreak > nul
    
    :: Start API nodes
    echo   - Starting %NODE_COUNT% API nodes...
    
    :: Calculate the last port
    set /a LAST_PORT=5185+%NODE_COUNT%
    
    :: Create appsettings files and start containers for each node
    for /l %%i in (1,1,%NODE_COUNT%) do (
        set NODE_ID=node%%i
        set NODE_REGION=region%%i
        set NODE_TAG=storage-%%i
        set /a NODE_PORT=5185+%%i
        set NODE_HOSTNAME=drocsid-storage-%%i
        set NODE_ENDPOINT=http://localhost:!NODE_PORT!
        
        echo     Setting up node%%i on port !NODE_PORT!...
        
        :: Create a copy of the template
        copy /Y "node-template.json" "node%%i-appsettings.json" >nul
        if !ERRORLEVEL! NEQ 0 (
            echo ERROR: Failed to copy template file for node%%i.
            exit /b 1
        )
        
        :: Replace placeholders in the config file
        powershell -Command "(Get-Content 'node%%i-appsettings.json') | ForEach-Object { $_ -replace 'NODE_ID_PLACEHOLDER', 'node%%i' -replace 'NODE_REGION_PLACEHOLDER', 'region%%i' -replace 'NODE_TAG_PLACEHOLDER', 'storage-%%i' -replace 'NODE_ENDPOINT_PLACEHOLDER', 'http://localhost:!NODE_PORT!' -replace 'NODE_HOSTNAME_PLACEHOLDER', 'drocsid-storage-%%i' } | Set-Content 'node%%i-appsettings.json'"
        if !ERRORLEVEL! NEQ 0 (
            echo ERROR: Failed to update configuration for node%%i.
            exit /b 1
        )
        
        :: Get the drocsid network name
        for /f %%n in ('docker network ls --filter "name=drocsid" --format "{{.Name}}" ^| findstr drocsid') do set NETWORK_NAME=%%n
        
        :: Stop and remove existing container if it exists
        docker rm -f "drocsid-api-%%i" >nul 2>&1
        
        :: Start the container
        docker run -d ^
            --name "drocsid-api-%%i" ^
            --network !NETWORK_NAME! ^
            -e ASPNETCORE_ENVIRONMENT=Production ^
            -e ASPNETCORE_URLS=http://+:5186 ^
            -p "!NODE_PORT!:5186" ^
            -v "%cd%\node%%i-appsettings.json:/app/appsettings.json" ^
            -v "api-node%%i-storage:/app/FileStorage" ^
            --restart on-failure ^
            drocsid-api >nul
        
        if !ERRORLEVEL! NEQ 0 (
            echo ERROR: Failed to start container for node%%i.
            exit /b 1
        )
    )
    
    echo   - All API nodes started successfully!
)

echo.
echo ===== Deployment Summary =====
if "%BUILD_BACKEND%"=="true" (
    echo Backend services:
    echo - Registry service: http://localhost:5261
    echo - API nodes: %NODE_COUNT% nodes running on ports 5186-!LAST_PORT!
    echo.
    echo Running containers:
    docker ps --format "table {{.Names}}\t{{.Ports}}\t{{.Status}}" | findstr drocsid
    echo.
)

if "%BUILD_UI%"=="true" (
    echo UI application:
    echo - Application: %cd%\build\UI\UI.exe
    echo - Launcher: %cd%\build\run-ui.bat
    echo - Desktop shortcut: Created
    echo.
)

:: Run UI if needed
if "%RUN_UI%"=="true" (
    if exist "build\UI\UI.exe" (
        echo Starting UI application...
        start "" "build\UI\UI.exe"
    ) else (
        echo UI application not found. Cannot start UI.
    )
)

echo.
echo ===== All Done! =====
echo.
echo For help, run: build-and-run.bat --help