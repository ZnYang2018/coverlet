
cls
@echo off

echo Build server code ==================================================================================================================================
pushd "D:\github\seismic\hello-world"
dotnet build all-projects.sln
popd

echo Injection coverage code ============================================================================================================================
pushd "D:\github\coverlet-coverage\coverlet\src\coverlet.console\bin\Debug\netcoreapp2.2\"
set coverlet=".\coverlet.console.dll"
set serverdll="D:\github\seismic\hello-world\src\Seismic.HelloWorld.Server\bin\Debug\netcoreapp2.2\Seismic.HelloWorld.Server.dll"
set clientdllfolder="D:\github\seismic\hello-world\src\Seismic.HelloWorld.Client\bin\Debug\netstandard2.0"
REM inject coverage code for server, model and client
dotnet %coverlet% inject %serverdll% --include-test-assembly --include-directory %clientdllfolder%
popd

echo Copy and overwrite the injected dll to test folder ===============================================================================================================
pushd "D:\github\seismic\hello-world"
set server=".\src\Seismic.HelloWorld.Server\bin\Debug\netcoreapp2.2\Seismic.HelloWorld.Server.dll"
set model=".\src\Seismic.HelloWorld.Server\bin\Debug\netcoreapp2.2\Seismic.HelloWorld.Model.dll"
set client=".\src\Seismic.HelloWorld.Client\bin\Debug\netstandard2.0\Seismic.HelloWorld.Client.dll"

rem client test
set clienttestfolder=".\test\Seismic.HelloWorld.Client.Tests\bin\Debug\netcoreapp2.2"
copy %model% %clienttestfolder% /Y
copy %client% %clienttestfolder% /Y

rem model test
set modeltestfolder=".\test\Seismic.HelloWorld.Model.Tests\bin\Debug\netcoreapp2.2"
copy %model% %modeltestfolder% /Y

rem server test
set servertestfolder=".\test\Seismic.HelloWorld.Server.Tests\bin\Debug\netcoreapp2.2"
copy %server% %servertestfolder% /Y
copy %model% %servertestfolder% /Y
popd

echo Run unit test  ====================================================================================================================================================
pushd "D:\github\seismic\hello-world\test"
rem run client test
dotnet test ".\Seismic.HelloWorld.Client.Tests\bin\Debug\netcoreapp2.2\Seismic.HelloWorld.Client.Tests.dll"
rem run model test
dotnet test ".\Seismic.HelloWorld.Model.Tests\bin\Debug\netcoreapp2.2\Seismic.HelloWorld.Model.Tests.dll"
rem run server test
dotnet test ".\Seismic.HelloWorld.Server.Tests\bin\Debug\netcoreapp2.2\Seismic.HelloWorld.Server.Tests.dll"
popd

echo Get coverage result ===============================================================================================================================================
pushd "D:\github\coverlet-coverage\coverlet\src\coverlet.console\bin\Debug\netcoreapp2.2\"
REM collect coverage result after unit test
dotnet %coverlet% collect CoverageResult --format opencover --format json

echo Get coverage report ===============================================================================================================================================
reportgenerator -reports:.\coverage.opencover.xml -targetdir:Report -reporttypes:Html
.\Report\index.html


echo Backup the coverage json result to merge with manual coverage =====================================================================================================
if not exist "Backup" mkdir "Backup"
copy coverage.json "Backup" /Y
echo "Backup done"
popd

echo Start service to do manual operation ==============================================================================================================================
rem it's better to start server outside the batch
pushd "D:\github\seismic\hello-world\src\Seismic.HelloWorld.Server\bin\Debug\netcoreapp2.2"
dotnet "Seismic.HelloWorld.Server.dll"
rem it will hang the batch until Ctrl+C
popd

echo Collect the new coverage result after manual operation ============================================================================================================
pushd "D:\github\coverlet-coverage\coverlet\src\coverlet.console\bin\Debug\netcoreapp2.2\"
REM collect coverage result after unit test
dotnet %coverlet% collect CoverageResult --format opencover --format json

echo Get final coverage report =========================================================================================================================================
reportgenerator -reports:.\coverage.opencover.xml -targetdir:Report -reporttypes:Html
.\Report\index.html
popd
