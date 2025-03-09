@echo off
setlocal enabledelayedexpansion

echo ===== Drocsid Cleanup =====

:: Stop and remove all API node containers
echo Stopping and removing API node containers...
for /f "tokens=*" %%i in ('docker ps -a -q --filter "name=drocsid-api"') do (
    docker rm -f %%i
)

:: Stop and remove infrastructure containers (postgres and registry)
echo Stopping infrastructure services...
docker-compose down

:: Check if volumes should be removed
if "%1"=="--volumes" (
    echo Removing API node volumes...
    for /f "tokens=*" %%i in ('docker volume ls --filter "name=api-node" -q') do (
        docker volume rm %%i
    )
    echo Volumes removed.
)

:: Cleanup generated appsettings files
echo Cleaning up generated appsettings files...
del /q node*-appsettings.json 2>nul

echo Cleanup complete!