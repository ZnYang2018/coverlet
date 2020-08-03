# How to use coverlet console

## Preparation  
You need all your code stuff including **source code**, compiled ***.dll** and ***.pdb** at their folder structure for example: **C:\YourProject**.  
If you want to run coverlet with the code stuff in other disk drive or folder or in another machine, you need to config source roots mapping.  
When you build your code at your machine, the .dll contains the information about what .pdb file it uses that is the absolute path to the .pdb.  
Also the .pdb contains the source code information which is absolute path.  
Once you move all the stuff to another place, the information mentioned still direct to the old path. So you need to tell coverlet the new path.  

For example you move all the code stuff to **D:\MyProject**:
  
Original path| New path
---|---
C:\YourProject\Class1.cs|D:\MyProject\Class1.cs
C:\YourProject\bin\Debug\Test.dll|D:\MyProject\bin\Debug\Test.dll
C:\YourProject\bin\Debug\Test.pdb|D:\MyProject\bin\Debug\Test.pdb

To tell coverlet you need to update file **CoverletSourceRootsMapping**  
Replace or add a new line or several lines:  
**ProjectName1**|**D:\\MyProject\\**=**C:\\YourProject\\**
**ProjectName2**|**D:\\TheirProjectB\\abc\\**=**C:\\YourProjectB\\abc\\**  
When coverlet get the pdb information _C:\YourProject\bin\Debug\Test.pdb_ from dll it will redirect to D:\MyProject\bin\Debug\Test.pdb

## Injection
Before we run app to get the coverage, we need to inject the coverage code to the .dll of you app that you want to cover.  
Run the command
```batch
dotnet coverlet.console.dll inject "C:\YourProject\bin\Debug\Test.dll" --include-test-assembly
```
After the command, you will find the *.dll and *.pdb files are updated.  
So the injection finished and there is a file named **CoverageResult** besides the coverlet tool.  
The file contains the injection result of all the *.dll, *.pdb and source code files which is used to collect coverage result.  

## Run the app
So the app's dll are injected. Run the app manually or automatically is ok to trigger the coverage logging.  

## Collection
After some operation on the app there may be some coverage info. 
There is no difference if you close the app or not.  
Run the command to collect
```batch
dotnet coverlet.console.dll collect CoverageResult --format opencover
```
The collection is the result of that time. You can event operation on the app again and continue to collect.   
It will generate a file **coverage.opencover.xml**. 

## Report
The result from collection is not readable. So we use another tool to get the report.  
* ReportGenerator installed with  
```batch
dotnet tool install reportgenerator
``` 
We use the result in collection
```batch
reportgenerator -reports:.\coverage.opencover.xml -targetdir:Report -reporttypes:Html
``` 
Then it will generate an html report to see the coverage detail information.