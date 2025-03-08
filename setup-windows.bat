@echo off
echo Setting up Drocsid Docker environment...

REM Make init-multiple-databases.sh executable for WSL
echo Converting line endings for bash scripts...
powershell -Command "(Get-Content init-multiple-databases.sh) | ForEach-Object { $_ -replace \"`r`n\", \"`n\" } | Set-Content -NoNewline init-multiple-databases.sh"

REM Create required directories
echo Creating required directories...
mkdir Core\Interfaces\Options 2>nul

REM Fix project files to prevent conflicts
echo Running PowerShell script to fix project files...
powershell -File fix-projects.ps1

REM Clean any previous Docker containers
echo Cleaning previous Docker containers...
docker-compose down

REM Start Docker Compose
echo Starting Docker Compose...
docker-compose up -d

REM Wait for services to start
echo Waiting for services to start...
timeout /T 10 /NOBREAK

REM Check services
echo Checking service status...
docker-compose ps

echo.
echo Setup complete! Your Drocsid distributed file storage system is now running!
echo.
echo Registry service: http://localhost:5261
echo API Node 1: http://localhost:5186
echo API Node 2: http://localhost:5187
echo API Node 3: http://localhost:5188
echo Access Swagger UI at http://localhost:5261/swagger or http://localhost:5186/swagger
echo.