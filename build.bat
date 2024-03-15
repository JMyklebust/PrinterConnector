@echo off
rem Set our working dir to the directory of the bat file
pushd %~dp0
rem Build a publish ready version
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=.\bin\Release\publish 

rem This will publish a "hidden" version of the application, by that it means that the console window will not be shown while running the program.
rem dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=.\bin\Release\publish-hidden -p:TargetType=WinExe
popd
