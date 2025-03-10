@echo off
setlocal enabledelayedexpansion

echo ===== Creating Mock Plugin Files =====

:: Create Plugins directory if it doesn't exist
mkdir Plugins 2>nul

:: Create empty WhiteboardPlugin.dll
echo Creating WhiteboardPlugin.dll (mock file)...
echo // This is a mock plugin file > Plugins\WhiteboardPlugin.cs
cd Plugins
dotnet new classlib -n WhiteboardPlugin
del /q WhiteboardPlugin\Class1.cs
copy WhiteboardPlugin.cs WhiteboardPlugin\WhiteboardPlugin.cs
cd WhiteboardPlugin
dotnet build -o ..
cd ..
rd /s /q WhiteboardPlugin
del WhiteboardPlugin.cs

:: Create empty VoiceChatPlugin.dll
echo Creating VoiceChatPlugin.dll (mock file)...
echo // This is a mock plugin file > VoiceChatPlugin.cs
dotnet new classlib -n VoiceChatPlugin
del /q VoiceChatPlugin\Class1.cs
copy VoiceChatPlugin.cs VoiceChatPlugin\VoiceChatPlugin.cs
cd VoiceChatPlugin
dotnet build -o ..
cd ..
rd /s /q VoiceChatPlugin
del VoiceChatPlugin.cs
cd ..

echo Mock plugin files created successfully!
echo.
echo These files are not functional plugins, but can be used with the 
echo modified PluginManagerService to register placeholder plugins.
echo.
echo Run the following API calls to load the plugins:
echo - POST http://localhost:5186/api/plugins/load?pluginName=WhiteboardPlugin
echo - POST http://localhost:5186/api/plugins/load?pluginName=VoiceChatPlugin