@echo off
echo.
echo ===== Drocsid Command Line Help =====
echo.
echo Available scripts:
echo.
echo build-and-run.bat [options]
echo   All-in-one script to build and run Drocsid
echo   Options:
echo     --no-backend    Skip building and running backend services
echo     --no-ui         Skip building the UI application
echo     --no-run-ui     Don't automatically launch the UI after building
echo     --nodes N       Deploy N API nodes (default: 3)
echo     --help          Show this help message
echo.
echo   Examples:
echo     build-and-run.bat                   - Build everything with default settings
echo     build-and-run.bat --nodes 5         - Build with 5 API nodes
echo     build-and-run.bat --no-ui           - Build only backend services
echo     build-and-run.bat --no-backend      - Build only UI application
echo.
echo Other commands:
echo.
echo run-ui.bat          - Quick launcher for the UI application
echo clean.bat           - Stop and clean up all containers and volumes
echo.
echo ===== Directory Structure =====
echo.
echo - build/UI/         - Built UI application
echo - node*-appsettings.json - Node configuration files
echo - Plugins/          - Plugin DLLs
echo.
echo ===== URLs and Ports =====
echo.
echo - Registry Service: http://localhost:5261
echo - API Nodes: http://localhost:5186, 5187, ...
echo.