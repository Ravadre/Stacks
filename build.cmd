@echo off
cls
.\tools\NuGet\nuget.exe install FAKE -OutputDirectory tools\ -ExcludeVersion
.\tools\FAKE\tools\Fake.exe Build.fsx "All"