@echo off
setlocal enabledelayedexpansion

echo ===== Building and Registering Drocsid Plugins =====

:: Check if API nodes are running
for /f %%i in ('docker ps ^| findstr drocsid-api ^| find /c /v ""') do set RUNNING_NODES=%%i

if %RUNNING_NODES%==0 (
    echo No Drocsid API nodes are running! Please deploy the application first.
    exit /b 1
)

:: Create Plugins directory if it doesn't exist
mkdir Plugins 2>nul

echo Building WhiteboardPlugin...
dotnet build WhiteboardPlugin/WhiteboardPlugin.csproj -c Release -o ./Plugins

echo Building VoiceChatPlugin...
dotnet build VoiceChatPlugin/VoiceChatPlugin.csproj -c Release -o ./Plugins

echo Plugin DLLs built successfully!

:: Copy plugins to all running API nodes
echo Copying plugins to all running API containers...

for /f "tokens=*" %%i in ('docker ps --filter "name=drocsid-api" --format "{{.Names}}"') do (
    echo Copying plugins to container: %%i
    
    :: Create the Plugins directory in the container if it doesn't exist
    docker exec %%i mkdir -p /app/Plugins
    
    :: Copy each plugin DLL to the container
    for %%f in (Plugins\*.dll) do (
        echo   - Copying %%~nxf...
        docker cp "Plugins\%%~nxf" %%i:/app/Plugins/
    )
)

echo.
echo Plugins copied to all containers. You can now load them via the API.
echo.
echo Use these API calls to verify the plugins:
echo - GET http://localhost:5186/api/plugins (should list available plugins)
echo - POST http://localhost:5186/api/plugins/load?pluginName=WhiteboardPlugin (to load whiteboard)
echo - POST http://localhost:5186/api/plugins/load?pluginName=VoiceChatPlugin (to load voice chat)
echo.
echo After loading, check these endpoints:
echo - GET http://localhost:5186/api/plugins/channel/{channelId}/communication
echo - GET http://localhost:5186/api/plugins/channel/{channelId}/collaboration