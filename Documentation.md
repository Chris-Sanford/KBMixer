# Documentation
Here is where I'll write down all my notes for each file as needed to ensure it's easy for me to pick back up this project as needed.

## Solution versus Project Relationship and Hierarchy

In Visual Studio, a **solution** is a container that can hold one or more **projects**. 

### Solution
- A solution (.sln file) is used to organize and manage multiple projects that are related or that work together to create a single application.
- It can include various types of projects, such as C# projects, database projects, and setup projects.
- Solutions help in managing the dependencies and build order of the projects they contain.

### Project
- A project (.csproj file for C#) contains all the files, resources, and configurations needed to build a specific component or application.
- Each project can produce an executable (.exe), a library (.dll, Dynamic Link Library), or other types of outputs.
- Projects define their own build settings, references, and dependencies.

In summary, a solution is a higher-level organizational unit that can encompass multiple projects, while a project is a more granular unit that defines the actual code and resources for a specific part of the application.

## KBMixer.sln

This is a Visual Studio solution file.

I'm documenting information on this here because you apparently cannot add comments on solution files.

### Format Version
- **Format Version:** 12.00  
    This indicates the version of the solution file format. It helps Visual Studio understand how to read and interpret the file.

### Visual Studio Version
- **Visual Studio Version:** 17.9.34728.123  
    This specifies the version of Visual Studio that was used to create or last modify the solution. It ensures compatibility with the specific features and settings of that version.

### Minimum Visual Studio Version
- **Minimum Visual Studio Version:** 10.0.40219.1  
    This indicates the minimum version of Visual Studio required to open the solution. If you try to open the solution with an older version, it may not work correctly.

### Project Definition
- **Project Type GUID:** {FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}  
    This is a unique identifier for the type of project. In this case, it represents a C# project.
- **Project Name:** KBMixer  
    The name of the project within the solution.
- **Project Path:** KBMixer.csproj  
    The relative path to the project file (.csproj), which contains the project-specific settings and configurations.
- **Project GUID:** {EAA644B9-95CA-48EC-9E0D-5F973F883307}  
    A unique identifier for the project within the solution. It helps Visual Studio manage and reference the project.

### Solution Configurations
- **Debug|Any CPU**  
- **Release|Any CPU**  
    These are the build configurations available for the solution. "Debug" is typically used for development and testing, providing more detailed error information. "Release" is optimized for performance and is used for the final product. "Any CPU" means the build can run on any CPU architecture (x86, x64, ARM).

### Project Configurations
- **Debug|Any CPU (Active and Build)**  
- **Release|Any CPU (Active and Build)**  
    These specify the configurations for building the project. "Active" means the configuration is currently selected, and "Build" means it will be built when the solution is built.

### Solution Properties
- **HideSolutionNode:** FALSE  
    This property determines whether the solution node is visible in the Solution Explorer. "FALSE" means the solution node is visible.

### Extensibility Globals
- **Solution GUID:** {0A297D55-F80F-447C-B53A-D9E2B14D1592}  
    A unique identifier for the solution itself. It helps Visual Studio and other tools reference the solution.

## .gitignore

This gitignore is from an official shared GitHub resource that catalogs all the core things to ignore in a Visual Studio solution repo that you wouldn't want committed to the repo itself. This is essentially temp files and binaries from builds or dependency packages.

This gitignore is mostly stock but I needed to add some things manually in order to effectively ignore all that was necessary.

## App.config

The `App.config` file is used to configure application settings, including runtime settings, connection strings, and other configuration data. It is an XML file that is read at runtime by the .NET Framework to configure the application.

#### Structure

- **Configuration Element**: The root element that contains all the configuration settings.
- **Startup Element**: Specifies the runtime version that the application supports.

#### Example

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
    <startup> 
        <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
    </startup>
</configuration>
```

#### Explanation

- **<?xml version="1.0" encoding="utf-8" ?>**: Declares the XML version and encoding used.
- **<configuration>**: The root element for all configuration settings.
- **<startup>**: Contains settings related to the application startup.
- **<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />**: Specifies that the application supports .NET Framework version 4.8.

This file is essential for defining how the application should behave at runtime and ensuring compatibility with the specified .NET Framework version.

## KBMixer.csproj

The `KBMixer.csproj` file is an XML file that contains the configuration and metadata needed to build the KBMixer project. Below is a detailed breakdown of its contents:

### XML Declaration
```xml
<?xml version="1.0" encoding="utf-8"?>
```
This line declares the XML version and the encoding used in the file.

### Project Element
```xml
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
```
The root element of the file, specifying the tools version and the XML namespace.

### Import Common Properties
```xml
<Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
```
Imports common MSBuild properties if they exist.

### PropertyGroup: General Configuration
```xml
<PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{EAA644B9-95CA-48EC-9E0D-5F973F883307}</ProjectGuid>
    <OutputType>Exe</OutputType>
    <RootNamespace>KBMixer</RootNamespace>
    <AssemblyName>KBMixer</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <Deterministic>true</Deterministic>
</PropertyGroup>
```
Defines general properties for the project:
- `Configuration` and `Platform`: Default to Debug and AnyCPU if not specified.
- `ProjectGuid`: Unique identifier for the project.
- `OutputType`: Specifies the output type as an executable.
- `RootNamespace` and `AssemblyName`: Set to KBMixer.
- `TargetFrameworkVersion`: Specifies .NET Framework version 4.8.
- `FileAlignment`: Sets file alignment.
- `AutoGenerateBindingRedirects` and `Deterministic`: Additional build settings.

### PropertyGroup: Debug Configuration
```xml
<PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
</PropertyGroup>
```
Defines properties specific to the Debug configuration:
- `PlatformTarget`: AnyCPU.
- `DebugSymbols` and `DebugType`: Enable full debugging.
- `Optimize`: Disabled for debugging.
- `OutputPath`: Specifies output directory for Debug build.
- `DefineConstants`: Defines DEBUG and TRACE constants.
- `ErrorReport` and `WarningLevel`: Set error reporting and warning level.

### PropertyGroup: Release Configuration
```xml
<PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
</PropertyGroup>
```
Defines properties specific to the Release configuration:
- `PlatformTarget`: AnyCPU.
- `DebugType`: PDB only for debugging.
- `Optimize`: Enabled for performance.
- `OutputPath`: Specifies output directory for Release build.
- `DefineConstants`: Defines TRACE constant.
- `ErrorReport` and `WarningLevel`: Set error reporting and warning level.

### ItemGroup: References
```xml
<ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xml.Linq" />
    <Reference Include="System.Data.DataSetExtensions" />
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="System.Data" />
    <Reference Include="System.Net.Http" />
    <Reference Include="System.Xml" />
</ItemGroup>
```
Lists the assemblies referenced by the project.

### ItemGroup: Compile
```xml
<ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
</ItemGroup>
```
Specifies the C# source files to be compiled.

### ItemGroup: None
```xml
<ItemGroup>
    <None Include="App.config" />
</ItemGroup>
```
Specifies non-compilable files included in the project.

### Import CSharp Targets
```xml
<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
```
Imports the C# targets file for building the project.

This file is essential for defining how the project is built and managed within Visual Studio.

## Properties/AssemblyInfo.cs

Describes various metadata on the assembly/binary that is built when compiling. Also contains a configuration for being able to interact with COM components which seems like a configuration thing beyond just metadata.