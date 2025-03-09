@echo off
echo Setting up Drocsid Docker environment...

REM Create init.sql file
echo Creating database initialization script...
echo -- Registry database for the central registry service > init.sql
echo CREATE DATABASE drocsid_registry; >> init.sql
echo. >> init.sql
echo -- Shared database for API nodes >> init.sql
echo CREATE DATABASE drocsid; >> init.sql

REM Clean any previous Docker containers
echo Cleaning previous Docker containers...
docker-compose down -v

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