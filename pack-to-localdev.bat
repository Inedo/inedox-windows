@echo off

dotnet new tool-manifest --force
dotnet tool install inedo.extensionpackager

cd Windows\InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\Windows.upack --build=Debug -o
cd ..\..